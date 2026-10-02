using System.ComponentModel;
using ErpMcp.Application.Common;
using ErpMcp.Application.Customers;
using ErpMcp.Application.Security;
using ErpMcp.Domain.Customers;
using ErpMcp.Server.Security;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Tools;

[McpServerToolType]
public sealed class CustomerTools(CustomerQueries customers)
{
    [RequiresScope(Scopes.CustomersRead)]
    [McpServerTool(Name = "search_customers", Title = "Search customers", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Find customer accounts by name, customer code or city. Returns a page of matches ordered by name.")]
    public Task<PagedResult<Customer>> SearchCustomers(
        [Description("Text to match against name, code or city (case-insensitive). Omit to list all.")] string? query = null,
        [Description("Filter by account status: active, on_hold or closed.")] string? status = null,
        [Description("Page size, 1-100. Default 20.")] int? limit = null,
        [Description("Number of results to skip, for paging.")] int? offset = null,
        CancellationToken ct = default) =>
        customers.SearchAsync(query, status, limit, offset, ct);

    [RequiresScope(Scopes.CustomersRead)]
    [McpServerTool(Name = "get_customer", Title = "Get customer", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Get one customer account with credit limit, status and a summary of their order history.")]
    public Task<CustomerDetails> GetCustomer(
        [Description("Customer code, e.g. CUST-0001.")] string customerCode,
        CancellationToken ct = default) =>
        customers.GetAsync(customerCode, ct);
}
