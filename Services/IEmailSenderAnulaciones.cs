using CBF_ORDENES_ANULADAS.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CBF_ORDENES_ANULADAS.Services
{
    public interface IEmailSenderAnulaciones
    {
        Task<bool> EnviarAlertaAnulacionAsync(List<OrdenAnuladaDTO> ordenesAnuladas, List<SuscriptorCorreoDTO> destinatarios);
    }
}

