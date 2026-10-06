using FluentValidation;
using Nexit.Application.DTOs.Clientes;
using Nexit.Core.Interfaces;

namespace Nexit.Application.Validators.Clientes;

public class CreateClienteValidator : AbstractValidator<CreateClienteDto>
{
    public CreateClienteValidator(IClienteRepository repository)
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(255);
        this.AddDineroRules(x => x.ValorReferenciaMonto, x => x.Moneda);
        RuleFor(x => x.Estado).Must(e => e is "Activo" or "Prospecto" or "Inactivo").WithMessage("Estado inválido");
        // Lista simple de correos, sin "principal" (2026-09-06) -- un cliente puede tener más de uno.
        // Cada elemento se valida como si fuera el único: formato de correo (ChildRules, no necesita
        // el cliente padre) y que no esté ya registrado en OTRO cliente (ExistsByEmailAsync mira toda
        // la tabla cliente_emails -- necesita el DTO padre para nada en Create, pero se usa la misma
        // forma de RuleForEach+MustAsync que en Update para que ambos validadores queden simétricos).
        RuleForEach(x => x.Emails).ChildRules(mail => mail.RuleFor(x => x.Email).NotEmpty().EmailAddress());
        RuleForEach(x => x.Emails)
            .MustAsync(async (mail, token) => string.IsNullOrWhiteSpace(mail.Email) || !await repository.ExistsByEmailAsync(mail.Email, null, token))
            .WithMessage("El email ya está registrado");
        // Teléfono NO es obligatorio (docs/32) -- el Excel del histórico real de proyectos nunca tuvo
        // teléfono de los clientes, y no hay ninguna razón de negocio para exigirlo. Si agregas uno,
        // igual no puede quedar vacío ni pasar de 50 caracteres.
        RuleForEach(x => x.Telefonos).ChildRules(phone => phone.RuleFor(x => x.Telefono).NotEmpty().MaximumLength(50));
    }
}
