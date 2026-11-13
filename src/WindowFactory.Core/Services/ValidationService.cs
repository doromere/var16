using System.Text.RegularExpressions;

namespace WindowFactory.Core.Services;

public sealed record ValidationReport(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
    public void ThrowIfInvalid() { if (!IsValid) throw new ValidationFailedException(Errors); }
}

public interface IValidationService
{
    bool IsValidPhone(string? phone);
    bool IsValidDocumentNumber(string? number);
    ValidationReport ValidateUnit(int widthMm, int heightMm, int sashes);
    ValidationReport ValidateHomeowner(string? fullName, string? address, int floor, string? phone);
    ValidationReport ValidateProfile(string? brand, int chambers, int depthMm, decimal pricePerMeter);
    ValidationReport ValidateGlazing(string? name, int thicknessMm, decimal pricePerM2);
}

/// <summary>Валидация вводимых данных (запрет отрицательных значений, Regex для телефона и номера).</summary>
public sealed class ValidationService : IValidationService
{
    private static readonly Regex Phone = new(@"^(\+7|8)[\s\-]?\(?\d{3}\)?[\s\-]?\d{3}[\s\-]?\d{2}[\s\-]?\d{2}$", RegexOptions.Compiled);
    private static readonly Regex DocNumber = new(@"^ПР23-ОКН-\d{4}-\d{5}$", RegexOptions.Compiled);

    public bool IsValidPhone(string? phone) => phone is not null && Phone.IsMatch(phone.Trim());
    public bool IsValidDocumentNumber(string? number) => number is not null && DocNumber.IsMatch(number);

    public ValidationReport ValidateUnit(int widthMm, int heightMm, int sashes)
    {
        var e = new List<string>();
        if (widthMm < ProductionLimits.MinWidthMm || widthMm > ProductionLimits.MaxWidthMm)
            e.Add($"Ширина должна быть от {ProductionLimits.MinWidthMm} до {ProductionLimits.MaxWidthMm} мм.");
        if (heightMm < ProductionLimits.MinHeightMm || heightMm > ProductionLimits.MaxHeightMm)
            e.Add($"Высота должна быть от {ProductionLimits.MinHeightMm} до {ProductionLimits.MaxHeightMm} мм.");
        if (sashes < ProductionLimits.MinSashes || sashes > ProductionLimits.MaxSashes)
            e.Add($"Количество створок: от {ProductionLimits.MinSashes} до {ProductionLimits.MaxSashes}.");
        return new ValidationReport(e);
    }

    public ValidationReport ValidateHomeowner(string? fullName, string? address, int floor, string? phone)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(fullName)) e.Add("Укажите ФИО заказчика.");
        if (string.IsNullOrWhiteSpace(address)) e.Add("Укажите адрес доставки.");
        if (floor < 1 || floor > 100) e.Add("Этаж должен быть от 1 до 100.");
        if (!IsValidPhone(phone)) e.Add("Телефон в формате +7 (900) 123-45-67 или 8 900 123 45 67.");
        return new ValidationReport(e);
    }

    public ValidationReport ValidateProfile(string? brand, int chambers, int depthMm, decimal pricePerMeter)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(brand)) e.Add("Укажите название профильной системы.");
        if (chambers < 1) e.Add("Количество камер должно быть положительным.");
        if (depthMm < 1) e.Add("Монтажная глубина должна быть положительной.");
        if (pricePerMeter < 0) e.Add("Цена за метр не может быть отрицательной.");
        return new ValidationReport(e);
    }

    public ValidationReport ValidateGlazing(string? name, int thicknessMm, decimal pricePerM2)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) e.Add("Укажите название стеклопакета.");
        if (thicknessMm < 1) e.Add("Толщина должна быть положительной.");
        if (pricePerM2 < 0) e.Add("Цена за м² не может быть отрицательной.");
        return new ValidationReport(e);
    }
}
