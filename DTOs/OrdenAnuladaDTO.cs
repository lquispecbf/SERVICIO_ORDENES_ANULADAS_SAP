using System;

namespace CBF_ORDENES_ANULADAS.DTOs
{
    public class OrdenAnuladaDTO
    {
        public int DocEntryPvd { get; set; }
        public int DocNumSap { get; set; }
        public int DocEntrySap { get; set; }
        public string? CardCode { get; set; }
        public string? CardName { get; set; }
        public string? EstadoAnterior { get; set; }
        public string? EstadoNuevo { get; set; }
        public bool TeniaMerch { get; set; }
        public bool MerchRevertido { get; set; }
        public string? Mensaje { get; set; }
        public DateTime FechaProceso { get; set; }
    }
}

