using CBF_ORDENES_ANULADAS.DTOs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace CBF_ORDENES_ANULADAS.Repositories
{
    public class OrdenesAnuladasRepo : IOrdenesAnuladasRepo
    {
        private readonly string _connectionString;
        private readonly string _bdHana;
        private readonly ILogger<OrdenesAnuladasRepo> _logger;

        public OrdenesAnuladasRepo(IConfiguration configuration, ILogger<OrdenesAnuladasRepo> logger)
        {
            _logger = logger;

            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada en appsettings.json.");

            _bdHana = configuration.GetConnectionString("BdHana") 
                ?? configuration["BdHana"] 
                ?? "TEST_PRUEBASET26";
        }

        public async Task<int> ObtenerVersionConfigAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("dbo.SP_OBTENER_VERSION_CONFIG_ALERTAS", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Convert.ToInt32(reader["VERSION_CONFIG"]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo verificar la versión de configuración en SQL Server: {msg}", ex.Message);
            }

            return 1;
        }

        public async Task<ConfiguracionRutinaDTO> ObtenerConfiguracionRutinaAsync(int defIntervalo, int defDiasAtras)
        {
            var config = new ConfiguracionRutinaDTO
            {
                IntervaloSegundos = defIntervalo >= 5 ? defIntervalo : 60,
                DiasAtras = defDiasAtras >= 0 ? defDiasAtras : 3,
                Habilitado = true,
                CanalCorreo = false,
                CanalIntranet = true
            };

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("dbo.SP_OBTENER_CONFIG_RUTINA", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add(new SqlParameter("@CODIGO_RUTINA", SqlDbType.VarChar, 50) { Value = "ANULACION_ORDENES_SAP" });

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    config.Habilitado = Convert.ToBoolean(reader["HABILITADO"]);
                    int intervalo = Convert.ToInt32(reader["INTERVALO_SEGUNDOS"]);
                    config.IntervaloSegundos = intervalo >= 5 ? intervalo : 5;
                    config.DiasAtras = Convert.ToInt32(reader["DIAS_ATRAS"]);
                    config.CanalCorreo = Convert.ToBoolean(reader["CANAL_CORREO"]);
                    config.CanalIntranet = Convert.ToBoolean(reader["CANAL_INTRANET"]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo leer la configuración de la rutina ANULACION_ORDENES_SAP desde la base de datos (usando valores locales): {msg}", ex.Message);
            }

            return config;
        }

        public async Task<List<SuscriptorCorreoDTO>> ObtenerSuscriptoresCorreoAsync(string codigoRutina = "ANULACION_ORDENES_SAP")
        {
            var lista = new List<SuscriptorCorreoDTO>();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("dbo.SP_OBTENER_SUSCRIPTORES_RUTINA_CORREO", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add(new SqlParameter("@CODIGO_RUTINA", SqlDbType.VarChar, 50) { Value = codigoRutina });

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string correo = reader["CORREO"]?.ToString()?.Trim() ?? string.Empty;
                    string nombre = reader["NOMBRE_COMPLETO"]?.ToString()?.Trim() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(correo) && correo.Contains("@"))
                    {
                        lista.Add(new SuscriptorCorreoDTO
                        {
                            Correo = correo,
                            NombreCompleto = nombre
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo obtener la lista de suscriptores de correo para {rutina}: {msg}", codigoRutina, ex.Message);
            }

            return lista;
        }

        public async Task<List<OrdenAnuladaDTO>> EjecutarAnulacionOrdenesAsync(int diasAtras)
        {
            var resultados = new List<OrdenAnuladaDTO>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                using var command = new SqlCommand("dbo.CBF_SP_ANULAR_ORDENES_CANCELADAS_SAP_PVD", connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 120 // 2 minutos máximo
                };

                command.Parameters.Add(new SqlParameter("@BD_HANA", SqlDbType.VarChar, 50) { Value = _bdHana });
                command.Parameters.Add(new SqlParameter("@DIAS_ATRAS", SqlDbType.Int) { Value = diasAtras });

                using var reader = await command.ExecuteReaderAsync();
                do
                {
                    bool tieneDocEntryPvd = false;
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (string.Equals(reader.GetName(i), "DOCENTRY_PVD", StringComparison.OrdinalIgnoreCase))
                        {
                            tieneDocEntryPvd = true;
                            break;
                        }
                    }

                    if (tieneDocEntryPvd)
                    {
                        while (await reader.ReadAsync())
                        {
                            resultados.Add(new OrdenAnuladaDTO
                            {
                                DocEntryPvd = Convert.ToInt32(reader["DOCENTRY_PVD"]),
                                DocNumSap = Convert.ToInt32(reader["DOCNUM_SAP"]),
                                DocEntrySap = Convert.ToInt32(reader["DOCENTRY_SAP"]),
                                CardCode = reader["CARDCODE"] != DBNull.Value ? reader["CARDCODE"].ToString() : null,
                                CardName = reader["CARDNAME"] != DBNull.Value ? reader["CARDNAME"].ToString() : null,
                                EstadoAnterior = reader["ESTADO_ANTERIOR"] != DBNull.Value ? reader["ESTADO_ANTERIOR"].ToString() : null,
                                EstadoNuevo = reader["ESTADO_NUEVO"] != DBNull.Value ? reader["ESTADO_NUEVO"].ToString() : null,
                                TeniaMerch = reader["TENIA_MERCH"] != DBNull.Value && Convert.ToBoolean(reader["TENIA_MERCH"]),
                                MerchRevertido = reader["MERCH_REVERTIDO"] != DBNull.Value && Convert.ToBoolean(reader["MERCH_REVERTIDO"]),
                                Mensaje = reader["MENSAJE"] != DBNull.Value ? reader["MENSAJE"].ToString() : null,
                                FechaProceso = Convert.ToDateTime(reader["FECHA_PROCESO"])
                            });
                        }
                    }
                } while (await reader.NextResultAsync());

                if (resultados.Any(r => r.EstadoAnterior == "ERROR"))
                {
                    var error = resultados.First(r => r.EstadoAnterior == "ERROR");
                    _logger.LogError("El SP devolvió un error controlado: {mensaje}", error.Mensaje);
                }
                else if (resultados.Count > 0)
                {
                    _logger.LogInformation(">>> ¡ÉXITO! Se detectaron y anularon {count} órdenes en PVD canceladas en SAP.", resultados.Count);
                }

                return resultados;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error técnico al ejecutar el procedimiento CBF_SP_ANULAR_ORDENES_CANCELADAS_SAP_PVD");
                throw;
            }
        }
    }
}

