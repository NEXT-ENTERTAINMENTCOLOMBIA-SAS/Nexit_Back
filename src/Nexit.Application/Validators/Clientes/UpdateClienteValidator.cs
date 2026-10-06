using FluentValidation;
using Nexit.Application.DTOs.Clientes;
using Nexit.Core.Interfaces;

namespace Nexit.Application.Validators.Clientes;

public class UpdateClienteValidator : AbstractValidator<UpdateClienteDto>
{
    public UpdateClienteValidator(IClienteRepository repository)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(255);
        this.AddDineroRules(x => x.ValorReferenciaMonto, x => x.Moneda);
        RuleFor(x => x.Estado).Must(e => e is "Activo" or "Prospecto" or "Inactivo").WithMessage("Estado inválido");
        // Ver el comentario equivalente en CreateClienteValidator -- misma lista simple de correos,
        // sin "principal". Al editar, se excluye este mismo cliente de la comprobación de duplicados
        // (dto.Id) para no rechazar un correo que ya era suyo -- por eso acá sí hace falta el DTO
        // padre dentro del MustAsync, a diferencia de Create.
        RuleForEach(x => x.Emails).ChildRules(mail => mail.RuleFor(x => x.Email).NotEmpty().EmailAddress());
        RuleForEach(x => x.Emails)
            .MustAsync(async (dto, mail, token) => string.IsNullOrWhiteSpace(mail.Email) || !await repository.ExistsByEmailAsync(mail.Email, dto.Id, token))
            .WithMessage("El email ya está registrado");
        // Teléfono NO es obligatorio (docs/32) -- ver el comentario equivalente en CreateClienteValidator.
        RuleForEach(x => x.Telefonos).ChildRules(phone => phone.RuleFor(x => x.Telefono).NotEmpty().MaximumLength(50));
    }
}
