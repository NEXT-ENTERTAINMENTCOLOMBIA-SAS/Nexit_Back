using FluentValidation;
using Nexit.Application.DTOs.Proveedores;
using Nexit.Core.Interfaces;

namespace Nexit.Application.Validators.Proveedores;

public class UpdateProveedorValidator : AbstractValidator<UpdateProveedorDto>
{
    public UpdateProveedorValidator(IProveedorRepository repository)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(255);
        this.AddDineroRules(x => x.CostoReferenciaValor, x => x.Moneda);
        RuleFor(x => x.PaisId).NotEmpty();
        RuleFor(x => x.CategoriaId).NotEmpty();
        // Lista simple de correos, sin "principal" (2026-09-06) -- ver el comentario detallado en
        // UpdateClienteValidator, mismo patrón aplicado acá a Proveedor.
        RuleForEach(x => x.Emails).ChildRules(mail => mail.RuleFor(x => x.Email).NotEmpty().EmailAddress());
        RuleForEach(x => x.Emails)
            .MustAsync(async (dto, mail, ct) => string.IsNullOrWhiteSpace(mail.Email) || !await repository.ExistsByEmailAsync(mail.Email, dto.Id, ct))
            .WithMessage("El email ya está registrado");
        RuleFor(x => x.Score).InclusiveBetween(1, 5).When(x => x.Score.HasValue);
        RuleForEach(x => x.Telefonos).ChildRules(phone => phone.RuleFor(x => x.Telefono).NotEmpty().MaximumLength(50));
    }
}
