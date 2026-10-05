namespace Nexit.Core.Entities;

/// <summary>
/// Una opción de las listas desplegables de Proyectos que antes vivían fijas en el código y en
/// CHECK constraints (tipo de proyecto, prioridad, sede, estado de la propuesta, área de seguimiento).
/// A propósito NO es una FK desde <see cref="Proyecto"/>: igual que <c>proveedores.estado</c>, el
/// proyecto sigue guardando el texto -- agregar/renombrar acá cambia lo que aparece en los
/// formularios sin tocar ni migrar los proyectos existentes (salvo al renombrar, que actualiza los
/// proyectos que usaban el valor viejo).
/// </summary>
public class OpcionConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Lista { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public short Orden { get; set; }
}
