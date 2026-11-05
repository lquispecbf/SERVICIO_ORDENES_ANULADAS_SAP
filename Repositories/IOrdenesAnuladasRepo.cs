using CBF_ORDENES_ANULADAS.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CBF_ORDENES_ANULADAS.Repositories
{
    public interface IOrdenesAnuladasRepo
    {
        Task<int> ObtenerVersionConfigAsync();
        Task<ConfiguracionRutinaDTO> ObtenerConfiguracionRutinaAsync(int defIntervalo, int defDiasAtras);
        Task<List<SuscriptorCorreoDTO>> ObtenerSuscriptoresCorreoAsync(string codigoRutina = "ANULACION_ORDENES_SAP");
        Task<List<OrdenAnuladaDTO>> EjecutarAnulacionOrdenesAsync(int diasAtras);
    }
}

