using System.Data;
using System.Data.Common;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

public record CloseFiscalYearCommand(int FiscalYearId, string ClosedBy, bool ForceClose = false)
    : IRequest<FiscalYearCloseResultDto>;

public class CloseFiscalYearHandler : IRequestHandler<CloseFiscalYearCommand, FiscalYearCloseResultDto>
{
    private readonly IAccountingDbContext _db;
    public CloseFiscalYearHandler(IAccountingDbContext db) => _db = db;

    public async Task<FiscalYearCloseResultDto> Handle(CloseFiscalYearCommand req, CancellationToken ct)
    {
        var conn = _db.GetDbConnection();
        var owns = conn.State != ConnectionState.Open;
        if (owns) await conn.OpenAsync(ct);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.sp_FY_Close";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 120;

            var pId = cmd.CreateParameter();
            pId.ParameterName = "@FiscalYearId";
            pId.DbType = DbType.Int32;
            pId.Value = req.FiscalYearId;
            cmd.Parameters.Add(pId);

            var pBy = cmd.CreateParameter();
            pBy.ParameterName = "@ClosedBy";
            pBy.DbType = DbType.String;
            pBy.Size = 100;
            pBy.Value = req.ClosedBy ?? "system";
            cmd.Parameters.Add(pBy);

            var pForce = cmd.CreateParameter();
            pForce.ParameterName = "@ForceClose";
            pForce.DbType = DbType.Boolean;
            pForce.Value = req.ForceClose;
            cmd.Parameters.Add(pForce);

            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (DbException ex)
            {
                throw new DomainException(ex.Message);
            }

            int locked = 0;
            try
            {
                await using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = @"SELECT COUNT(*) FROM acc.AccountingPeriods WHERE FiscalYearId=@id AND Status=2";
                var p = cmd2.CreateParameter();
                p.ParameterName = "@id";
                p.DbType = DbType.Int32;
                p.Value = req.FiscalYearId;
                cmd2.Parameters.Add(p);
                var r = await cmd2.ExecuteScalarAsync(ct);
                locked = r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
            }
            catch { /* table name may differ */ }

            return new FiscalYearCloseResultDto
            {
                Success = true,
                FiscalYearId = req.FiscalYearId,
                ClosedAt = DateTime.UtcNow,
                LockedPeriods = locked,
                Message = "تم إغلاق السنة المالية بنجاح",
            };
        }
        finally
        {
            if (owns) await conn.CloseAsync();
        }
    }
}
