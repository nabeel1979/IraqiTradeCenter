using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Store.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Store : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "store");

            migrationBuilder.CreateTable(
                name: "CommissionTransactions",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesRepId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalSales = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JournalEntryId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlatformUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformTraderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AssignedSalesRepId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncomingOrders",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlatformOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformOrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    AssignedSalesRepId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedInvoiceId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentsReceived",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CashAccountId = table.Column<int>(type: "int", nullable: false),
                    JournalEntryId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentsReceived", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesInvoices",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    SalesRepId = table.Column<int>(type: "int", nullable: true),
                    IncomingOrderId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JournalEntryId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesReps",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CommissionType = table.Column<int>(type: "int", nullable: false),
                    FixedCommissionRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncomingOrderItems",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncomingOrderId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UnitOfMeasureId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomingOrderItems_IncomingOrders_IncomingOrderId",
                        column: x => x.IncomingOrderId,
                        principalSchema: "store",
                        principalTable: "IncomingOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesInvoiceLines",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UnitOfMeasureId = table.Column<int>(type: "int", nullable: false),
                    UnitName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    QuantityInBase = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    LineDiscount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceLines_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalSchema: "store",
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommissionTiers",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesRepId = table.Column<int>(type: "int", nullable: false),
                    FromSalesAmount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ToSalesAmount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionTiers_SalesReps_SalesRepId",
                        column: x => x.SalesRepId,
                        principalSchema: "store",
                        principalTable: "SalesReps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTiers_SalesRepId",
                schema: "store",
                table: "CommissionTiers",
                column: "SalesRepId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_PeriodStart_PeriodEnd",
                schema: "store",
                table: "CommissionTransactions",
                columns: new[] { "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_SalesRepId",
                schema: "store",
                table: "CommissionTransactions",
                column: "SalesRepId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Code",
                schema: "store",
                table: "Customers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                schema: "store",
                table: "Customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PlatformTraderId",
                schema: "store",
                table: "Customers",
                column: "PlatformTraderId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PlatformUserId",
                schema: "store",
                table: "Customers",
                column: "PlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingOrderItems_IncomingOrderId",
                schema: "store",
                table: "IncomingOrderItems",
                column: "IncomingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingOrderItems_ItemId",
                schema: "store",
                table: "IncomingOrderItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingOrders_CustomerId",
                schema: "store",
                table: "IncomingOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingOrders_PlatformOrderId",
                schema: "store",
                table: "IncomingOrders",
                column: "PlatformOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomingOrders_Status",
                schema: "store",
                table: "IncomingOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentsReceived_CustomerId",
                schema: "store",
                table: "PaymentsReceived",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentsReceived_PaymentDate",
                schema: "store",
                table: "PaymentsReceived",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentsReceived_ReceiptNumber",
                schema: "store",
                table: "PaymentsReceived",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentsReceived_SalesInvoiceId",
                schema: "store",
                table: "PaymentsReceived",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceLines_ItemId",
                schema: "store",
                table: "SalesInvoiceLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceLines_SalesInvoiceId",
                schema: "store",
                table: "SalesInvoiceLines",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CustomerId",
                schema: "store",
                table: "SalesInvoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_InvoiceDate",
                schema: "store",
                table: "SalesInvoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_InvoiceNumber",
                schema: "store",
                table: "SalesInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_JournalEntryId",
                schema: "store",
                table: "SalesInvoices",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_SalesRepId",
                schema: "store",
                table: "SalesInvoices",
                column: "SalesRepId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_Status",
                schema: "store",
                table: "SalesInvoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReps_EmployeeCode",
                schema: "store",
                table: "SalesReps",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesReps_UserId",
                schema: "store",
                table: "SalesReps",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommissionTiers",
                schema: "store");

            migrationBuilder.DropTable(
                name: "CommissionTransactions",
                schema: "store");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "store");

            migrationBuilder.DropTable(
                name: "IncomingOrderItems",
                schema: "store");

            migrationBuilder.DropTable(
                name: "PaymentsReceived",
                schema: "store");

            migrationBuilder.DropTable(
                name: "SalesInvoiceLines",
                schema: "store");

            migrationBuilder.DropTable(
                name: "SalesReps",
                schema: "store");

            migrationBuilder.DropTable(
                name: "IncomingOrders",
                schema: "store");

            migrationBuilder.DropTable(
                name: "SalesInvoices",
                schema: "store");
        }
    }
}
