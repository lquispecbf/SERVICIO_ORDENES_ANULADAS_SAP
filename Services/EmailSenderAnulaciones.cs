using CBF_ORDENES_ANULADAS.Configuration;
using CBF_ORDENES_ANULADAS.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace CBF_ORDENES_ANULADAS.Services
{
    public class EmailSenderAnulaciones : IEmailSenderAnulaciones
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<EmailSenderAnulaciones> _logger;

        public EmailSenderAnulaciones(IOptions<SmtpSettings> settings, ILogger<EmailSenderAnulaciones> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> EnviarAlertaAnulacionAsync(List<OrdenAnuladaDTO> ordenesAnuladas, List<SuscriptorCorreoDTO> destinatarios)
        {
            if (ordenesAnuladas == null || ordenesAnuladas.Count == 0)
                return false;

            if (destinatarios == null || destinatarios.Count == 0)
            {
                _logger.LogWarning("No hay destinatarios de correo configurados para la rutina ANULACION_ORDENES_SAP.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.Password) || _settings.Password == "@")
            {
                _logger.LogWarning("Configuración SMTP incompleta en appsettings.json. Se omite el envío de correo.");
                return false;
            }

            try
            {
                string asunto = ordenesAnuladas.Count == 1
                    ? $"ALERTA CBF: Orden #{ordenesAnuladas[0].DocNumSap} Anulada en SAP y Cancelada en PVD"
                    : $"ALERTA CBF: {ordenesAnuladas.Count} Órdenes Anuladas en SAP y Canceladas en PVD";

                string cuerpoHtml = ConstruirCuerpoHtml(ordenesAnuladas);

                using var message = new MailMessage
                {
                    From = new MailAddress(_settings.From, _settings.FromName),
                    Subject = asunto,
                    Body = cuerpoHtml,
                    IsBodyHtml = true,
                    SubjectEncoding = Encoding.UTF8,
                    BodyEncoding = Encoding.UTF8
                };

                foreach (var dest in destinatarios)
                {
                    if (!string.IsNullOrWhiteSpace(dest.Correo))
                    {
                        message.To.Add(dest.Correo.Trim());
                    }
                }

                if (message.To.Count == 0)
                {
                    _logger.LogWarning("No se encontraron correos válidos en la lista de suscriptores.");
                    return false;
                }

                using var smtp = new SmtpClient(_settings.Host, _settings.Port)
                {
                    EnableSsl = _settings.EnableSsl,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 20000
                };

                if (!string.IsNullOrWhiteSpace(_settings.User) && !string.IsNullOrWhiteSpace(_settings.Password))
                {
                    smtp.Credentials = new NetworkCredential(_settings.User.Trim(), _settings.Password.Trim());
                }

                await smtp.SendMailAsync(message);

                _logger.LogInformation(">>> Correo de notificación de anulación enviado exitosamente a {Total} destinatario(s).", message.To.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de alerta de anulación: {Mensaje}", ex.Message);
                return false;
            }
        }

        private string ConstruirCuerpoHtml(List<OrdenAnuladaDTO> ordenes)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html lang='es'>");
            sb.Append("<head><meta charset='UTF-8'>");
            sb.Append("<style>");
            sb.Append("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f6f9; margin: 0; padding: 20px; color: #333; }");
            sb.Append(".container { max-width: 900px; margin: 0 auto; background-color: #fff; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); overflow: hidden; border-top: 5px solid #dc3545; }");
            sb.Append(".header { background-color: #212529; color: #fff; padding: 20px; text-align: center; }");
            sb.Append(".header h2 { margin: 0; font-size: 22px; font-weight: 600; color: #ffc107; }");
            sb.Append(".header p { margin: 5px 0 0; font-size: 13px; color: #adb5bd; }");
            sb.Append(".content { padding: 25px; }");
            sb.Append(".alert-box { background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 14px 18px; margin-bottom: 20px; border-radius: 4px; font-size: 13px; color: #664d03; line-height: 1.5; }");
            sb.Append("table { width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 12px; }");
            sb.Append("th { background-color: #343a40; color: #fff; text-align: left; padding: 10px; font-weight: 600; }");
            sb.Append("td { padding: 10px; border-bottom: 1px solid #dee2e6; vertical-align: middle; }");
            sb.Append("tr:nth-child(even) { background-color: #f8f9fa; }");
            sb.Append(".badge-danger { background-color: #f8d7da; color: #842029; border: 1px solid #f5c2c7; padding: 4px 8px; border-radius: 4px; font-weight: 600; font-size: 11px; display: inline-block; }");
            sb.Append(".badge-success { background-color: #d1e7dd; color: #0f5132; border: 1px solid #badbcc; padding: 4px 8px; border-radius: 4px; font-weight: 600; font-size: 11px; display: inline-block; }");
            sb.Append(".badge-secondary { background-color: #e2e3e5; color: #41464b; border: 1px solid #d3d6d8; padding: 4px 8px; border-radius: 4px; font-weight: 500; font-size: 11px; display: inline-block; }");
            sb.Append(".badge-warning { background-color: #fff3cd; color: #664d03; border: 1px solid #ffecb5; padding: 4px 8px; border-radius: 4px; font-weight: 600; font-size: 11px; display: inline-block; }");
            sb.Append(".footer { background-color: #f8f9fa; border-top: 1px solid #e9ecef; padding: 15px; text-align: center; font-size: 11px; color: #6c757d; }");
            sb.Append("</style></head><body>");

            sb.Append("<div class='container'>");
            sb.Append("<div class='header'>");
            sb.Append("<h2>NOTIFICACIÓN CBF: Órdenes Anuladas en SAP B1</h2>");
            sb.Append("<p>Servicio Automático de Monitoreo y Sincronización PVD</p>");
            sb.Append("</div>");

            sb.Append("<div class='content'>");
            sb.Append("<div class='alert-box'>");
            sb.Append("<strong>Aviso del Sistema:</strong> Se detectaron órdenes de venta anuladas o canceladas en SAP Business One que aún figuraban activas en el Punto de Venta (PVD).<br>");
            sb.Append("<strong>Acción Realizada:</strong> El servicio procedió a cancelar la orden en PVD de manera automática para evitar facturaciones o despachos erróneos, reincorporando el stock de Merchandising físico al Almacén Nicolás Arriola si correspondía.");
            sb.Append("</div>");

            sb.Append("<table>");
            sb.Append("<thead><tr>");
            sb.Append("<th>Pedido SAP</th>");
            sb.Append("<th>Orden PVD</th>");
            sb.Append("<th>Cliente</th>");
            sb.Append("<th>Estado Anterior PVD</th>");
            sb.Append("<th>Estado Actual PVD</th>");
            sb.Append("<th>Merchandising</th>");
            sb.Append("<th>Fecha y Hora</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var item in ordenes)
            {
                sb.Append("<tr>");
                sb.Append($"<td><strong style='color:#dc3545; font-size: 13px;'>#{item.DocNumSap}</strong></td>");
                sb.Append($"<td><strong style='color:#0d6efd;'>#{item.DocEntryPvd}</strong></td>");
                sb.Append($"<td><strong>{item.CardCode}</strong><br><span style='color:#555;'>{WebUtility.HtmlEncode(item.CardName ?? "")}</span></td>");
                sb.Append($"<td><span class='badge-warning'>{FormatearEstadoAnterior(item.EstadoAnterior)}</span></td>");
                sb.Append("<td><span class='badge-danger'>Cancelado en PVD</span></td>");

                if (item.TeniaMerch)
                {
                    if (item.MerchRevertido)
                    {
                        sb.Append("<td><span class='badge-success'>✓ Revertido a Nicolás Arriola</span></td>");
                    }
                    else
                    {
                        sb.Append("<td><span class='badge-danger'>⚠ Pendiente Reversión</span></td>");
                    }
                }
                else
                {
                    sb.Append("<td><span class='badge-secondary'>No aplica (Sin Merch)</span></td>");
                }

                sb.Append($"<td>{item.FechaProceso:yyyy-MM-dd HH:mm}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
            sb.Append("</div>");

            sb.Append("<div class='footer'>");
            sb.Append($"<p>Este es un reporte automático generado por el servicio CBF_ORDENES_ANULADAS a las {DateTime.Now:yyyy-MM-dd HH:mm:ss}.</p>");
            sb.Append("<p>Por favor no responda a este correo electrónico.</p>");
            sb.Append("</div>");
            sb.Append("</div></body></html>");

            return sb.ToString();
        }

        private static string FormatearEstadoAnterior(string? estadoRaw)
        {
            if (string.IsNullOrWhiteSpace(estadoRaw))
                return "Activo en PVD";

            string upper = estadoRaw.ToUpperInvariant();
            if (upper.Contains("DOCSTATUS=O"))
                return "Abierto / Activo en PVD";
            if (upper.Contains("DOCSTATUS=W"))
                return "Activo en PVD (Sincronizado SAP)";
            if (upper.Contains("DOCSTATUS=C"))
                return "Cancelado en PVD";

            return "Activo en PVD";
        }
    }
}

