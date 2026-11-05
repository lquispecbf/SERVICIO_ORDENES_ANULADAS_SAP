using CBF_ORDENES_ANULADAS.DTOs;
using CBF_ORDENES_ANULADAS.Repositories;
using CBF_ORDENES_ANULADAS.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CBF_ORDENES_ANULADAS
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private bool _enEjecucion = false;

        public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int versionLocal = -1;
            int intervalo = _configuration.GetValue<int>("IntervaloSegundos", 60);
            int diasAtras = _configuration.GetValue<int>("DiasAtras", 3);

            _logger.LogInformation("Servicio Windows CBF_ORDENES_ANULADAS iniciado a las {Fecha:yyyy-MM-dd HH:mm:ss}", DateTime.Now);
            _logger.LogInformation("Parámetros base: Intervalo = {intervalo}s | Rango = últimos {dias} días.", intervalo, diasAtras);

            var ultimaEjecucion = DateTime.MinValue;
            ConfiguracionRutinaDTO? configRutina = null;
            bool estadoAnteriorHabilitado = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IOrdenesAnuladasRepo>();
                    var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSenderAnulaciones>();

                    // 1. Verificación en tiempo real de cambios en Intranet (Hot-Reload cada 3 segundos)
                    int versionRemota = await repo.ObtenerVersionConfigAsync();
                    if (versionRemota != versionLocal || configRutina == null)
                    {
                        configRutina = await repo.ObtenerConfiguracionRutinaAsync(intervalo, diasAtras);
                        versionLocal = versionRemota;

                        intervalo = configRutina.IntervaloSegundos;
                        diasAtras = configRutina.DiasAtras;

                        _logger.LogInformation(">>> [HOT-RELOAD] Configuración sincronizada desde Intranet (Versión: {ver}). Habilitado: {hab}, Intervalo: {inter}s, Días atrás: {dias}, Correo: {correo}",
                            versionLocal, configRutina.Habilitado, intervalo, diasAtras, configRutina.CanalCorreo);

                        estadoAnteriorHabilitado = configRutina.Habilitado;
                    }

                    // 2. Si la rutina fue desactivada desde la Intranet, pausar temporalmente
                    if (!configRutina.Habilitado)
                    {
                        if (estadoAnteriorHabilitado)
                        {
                            _logger.LogWarning("La rutina ANULACION_ORDENES_SAP fue DESACTIVADA desde la Intranet. En espera de reactivación...");
                            estadoAnteriorHabilitado = false;
                        }

                        await Task.Delay(3000, stoppingToken);
                        continue;
                    }

                    estadoAnteriorHabilitado = true;

                    // 3. Evaluar si ya transcurrió el intervalo en segundos
                    double segundosTranscurridos = (DateTime.Now - ultimaEjecucion).TotalSeconds;
                    if (segundosTranscurridos >= intervalo && !_enEjecucion)
                    {
                        _enEjecucion = true;
                        var inicioProceso = DateTime.Now;

                        try
                        {
                            _logger.LogInformation("Escaneando órdenes canceladas en SAP HANA vs PVD [Rango: {dias} días]...", diasAtras);

                            var resultados = await repo.EjecutarAnulacionOrdenesAsync(diasAtras);
                            var anuladas = resultados.Where(r => r.DocEntryPvd > 0).ToList();

                            if (anuladas.Count == 0)
                            {
                                _logger.LogInformation("Sin órdenes canceladas pendientes de sincronizar en PVD. Todo sincronizado.");
                            }
                            else
                            {
                                _logger.LogInformation(">>> ÓRDENES CANCELADAS EN PVD: {count}", anuladas.Count);
                                foreach (var item in anuladas)
                                {
                                    string infoMerch = item.TeniaMerch 
                                        ? (item.MerchRevertido ? " [MERCH REVERTIDO A ALMACEN]" : " [MERCH ERROR REVERSION]")
                                        : " [SIN MERCH]";

                                    _logger.LogInformation(
                                        "DOCENTRY PVD: {pvd} | SAP DOCNUM: {sap} | CLIENTE: {cli} | {ant} -> {nuevo}{merch}",
                                        item.DocEntryPvd, item.DocNumSap, item.CardCode, item.EstadoAnterior, item.EstadoNuevo, infoMerch
                                    );
                                }

                                // 4. Envío de correos si el canal está habilitado para esta rutina
                                if (configRutina.CanalCorreo)
                                {
                                    var suscriptores = await repo.ObtenerSuscriptoresCorreoAsync("ANULACION_ORDENES_SAP");
                                    if (suscriptores.Count > 0)
                                    {
                                        _logger.LogInformation("Despachando correo de notificación a {Count} suscriptor(es)...", suscriptores.Count);
                                        await emailSender.EnviarAlertaAnulacionAsync(anuladas, suscriptores);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Canal Correo activado en Intranet, pero no hay suscriptores de correo registrados para ANULACION_ORDENES_SAP.");
                                    }
                                }
                            }

                            ultimaEjecucion = DateTime.Now;
                            var duracion = (DateTime.Now - inicioProceso).TotalSeconds;
                            _logger.LogInformation("Ciclo finalizado en {segundos:F2}s. Siguiente escaneo en {intervalo}s.", duracion, intervalo);
                        }
                        finally
                        {
                            _enEjecucion = false;
                        }
                    }

                    // Intervalo de evaluación de Hot-Reload (cada 3 segundos)
                    await Task.Delay(3000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error no controlado en el ciclo del servicio CBF_ORDENES_ANULADAS");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("Servicio Windows CBF_ORDENES_ANULADAS detenido a las {Fecha:yyyy-MM-dd HH:mm:ss}", DateTime.Now);
        }
    }
}

