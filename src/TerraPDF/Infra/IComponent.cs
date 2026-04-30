namespace TerraPDF.Infra;

/// <summary>Represents a reusable content component that can be injected into any container.</summary>
/// <example>
/// <code>
/// class InvoiceHeader : IComponent
/// {
///     public void Compose(IContainer container) =>
///         container.Background(Colors.Blue.Medium).Padding(10).Text("INVOICE");
/// }
///
/// column.Item().Component(new InvoiceHeader());
/// </code>
/// </example>
public interface IComponent
{
    void Compose(IContainer container);
}
