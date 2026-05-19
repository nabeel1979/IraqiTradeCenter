namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;

public class CurrencyRateBulletinDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string BaseCurrency { get; set; } = default!;
    public DateTime EffectiveAt { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = default!;
    public DateTime? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<CurrencyRateLineDto> Lines { get; set; } = new();
    /// <summary>هل هذه النشرة هي النشرة المنشورة الافتراضية الحالية (الأحدث Effective)؟</summary>
    public bool IsDefault { get; set; }
}

public class CurrencyRateLineDto
{
    public int Id { get; set; }
    public string Currency { get; set; } = default!;
    public decimal Rate { get; set; }
    public int Operation { get; set; }
    /// <summary>"Multiply" أو "Divide"</summary>
    public string OperationText { get; set; } = default!;
    public string? Notes { get; set; }
}

public class CurrencyRateLinePayload
{
    public string Currency { get; set; } = default!;
    public decimal Rate { get; set; }
    /// <summary>1 = Multiply, 2 = Divide</summary>
    public int Operation { get; set; } = 1;
    public string? Notes { get; set; }
}
