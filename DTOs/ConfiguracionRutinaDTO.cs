namespace CBF_ORDENES_ANULADAS.DTOs
{
    public class ConfiguracionRutinaDTO
    {
        public bool Habilitado { get; set; } = true;
        public int IntervaloSegundos { get; set; } = 60;
        public int DiasAtras { get; set; } = 3;
        public bool CanalCorreo { get; set; } = false;
        public bool CanalIntranet { get; set; } = true;
    }
}

