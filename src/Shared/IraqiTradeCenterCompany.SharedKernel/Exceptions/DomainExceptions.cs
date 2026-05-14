namespace IraqiTradeCenterCompany.SharedKernel.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string entity, object key)
        : base($"{entity} برقم {key} غير موجود") { }
}
