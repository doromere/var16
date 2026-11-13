using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WindowFactory.Core.Models;

namespace WindowFactory.Core.Services;

public interface IIntegrityService
{
    string BuildCanonicalString(string number, DateTime date, string counterparty, decimal total, int itemCount);
    string ComputeSha256(string text);
    string ComputeForOrder(WindowOrder order);
}

/// <summary>SHA256 от конкатенации [Номер]+[Дата]+[Контрагент]+[Сумма]+[КоличествоПозиций] (через «|»).</summary>
public sealed class IntegrityService : IIntegrityService
{
    public const string Separator = "|";

    public string BuildCanonicalString(string number, DateTime date, string counterparty, decimal total, int itemCount)
        => string.Join(Separator,
            number.Trim(),
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            counterparty.Trim(),
            total.ToString("F2", CultureInfo.InvariantCulture),
            itemCount.ToString(CultureInfo.InvariantCulture));

    public string ComputeSha256(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public string ComputeForOrder(WindowOrder order)
        => ComputeSha256(BuildCanonicalString(order.OrderNum, order.OrderDate,
            order.Homeowner?.FullName ?? string.Empty, order.TotalPrice, order.OrderItems.Count));
}
