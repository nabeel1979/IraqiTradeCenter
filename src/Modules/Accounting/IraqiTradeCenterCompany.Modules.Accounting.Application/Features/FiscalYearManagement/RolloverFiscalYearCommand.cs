using System.Data;
using System.Data.Common;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

public record RolloverFiscalYearCommand(
    int SourceFiscalYearId,
    int TargetFiscalYearId,
    string PerformedBy,
    string RetainedEarningsCode,
    bool PreviewOnly = false
) : IRequest<FiscalYearRolloverResultDto>;

public class RolloverFiscalYearHandler : IRequestHandler<RolloverFiscalYearCommand, FiscalYearRolloverResultDto>
{
    private readonly IAccountingDbContext _db;
    public RolloverFiscalYearHandler(IAccountingDbContext db) => _db = db;

    public async Task<FiscalYearRolloverResultDto> Handle(RolloverFiscalYearCommand req, CancellationToken ct)
    {
        var conn = _db.GetDbConnection();
        var owns = conn.State != ConnectionState.Open;
        if (owns) await conn.OpenAsync(ct);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.sp_FY_Rollover";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 300;

            void AddP(string name, DbType type, object value, int size = 0)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.DbType = type;
                if (size > 0) p.Size = size;
                p.Value = value ?? (object)DBNull.Value;
                cmd.Parameters.Add(p);
            }

            AddP("@SourceFiscalYearId", DbType.Int32, req.SourceFiscalYearId);
            AddP("@TargetFiscalYearId", DbType.Int32, req.TargetFiscalYearId);
            AddP("@PerformedBy", DbType.String, req.PerformedBy ?? "system", 100);
            AddP("@RetainedEarningsCode", DbType.String, req.RetainedEarningsCode ?? "3100", 50);
            AddP("@PreviewOnly", DbType.Boolean, req.PreviewOnly);

            int rolled = 0;
            decimal retained = 0m;

            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    rolled++;
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (reader.IsDBNull(i)) continue;
                        var val = reader.GetValue(i);
                        if (val is decimal d) retained += d;
                        else if (val is double dbl) retained += (decimal)dbl;
                    }
                }
                while (await reader.NextResultAsync(ct)) { }
            }
            catch (DbException ex)
            {
                throw new DomainException(ex.Message);
            }

            return new FiscalYearRolloverResultDto
            {
                Success = true,
                FromFiscalYearId = req.SourceFiscalYearId,
                ToFiscalYearId = req.TargetFiscalYearId,
                BalanceSheetAccountsRolled = rolled,
                RetainedEarningsTransferred = retained,
                Message = req.PreviewOnly
                    ? $"معاينة: سيتم تدوير {rolled} حساب"
                    : $"تم تدوير {rolled} حساب بنجاح",
            };
        }
        finally
        {
            if (owns) await conn.CloseAsync();
        }
    }
}
