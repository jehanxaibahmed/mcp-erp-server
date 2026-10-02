using System.ComponentModel;
using ErpMcp.Server.Security;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Resources;

/// <summary>
/// Orientation for the model: vocabulary, code formats and the rules it works under.
/// </summary>
/// <remarks>
/// ERP data is deliberately <em>not</em> exposed as resources. Resources are read without tool
/// calls, so data served that way would bypass the per-tool scope checks and the audit trail.
/// Data stays behind tools; resources only describe how to use them.
/// </remarks>
[McpServerResourceType]
public sealed class GuideResource(GrantedScopes granted, ScopeCatalog catalog)
{
    public const string Uri = "erp://guide";

    [McpServerResource(UriTemplate = Uri, Name = "erp_guide", Title = "ERP guide", MimeType = "text/markdown")]
    [Description("How this ERP is organised: code formats, order statuses, approval rules, and the tools this server allows.")]
    public string Read()
    {
        var tools = catalog.ScopeByTool
            .Where(t => catalog.IsToolAllowed(t.Key, granted.Scopes))
            .Select(t => $"- `{t.Key}`")
            .Order(StringComparer.Ordinal);

        return $"""
            # ERP guide

            A food-service wholesaler: trade customers, a product catalogue, stock in three
            warehouses, and sales orders. All amounts are GBP excluding VAT.

            ## Codes
            | Thing | Format | Example |
            | --- | --- | --- |
            | Customer | `CUST-` + 4 digits | `CUST-0003` |
            | Product SKU | 3 letters + `-` + 4 digits | `BEV-0001` |
            | Order | `SO-` + 6+ digits | `SO-100061` |
            | Warehouse | `WH-` + 3 letters | `WH-MAN` (Manchester), `WH-BHM` (Birmingham), `WH-LDS` (Leeds) |

            Codes are case-insensitive. Look them up with the search tools rather than guessing.

            ## Stock
            *Available* = on hand − reserved. Stock is reserved when an order is approved and leaves
            when it is fulfilled. Below the reorder level means it is time to restock.

            ## Orders
            `pending_approval` → `approved` → `fulfilled`, or `rejected` / `cancelled`.
            You can only create drafts (`pending_approval`). A person approves, rejects, fulfils or
            cancels, so never tell a user an order has been placed. Say it is awaiting approval.

            ## Customer accounts
            Only `active` accounts can order. `on_hold` and `closed` accounts cannot.

            ## Tools available on this server
            Granted scopes: {string.Join(", ", granted.Scopes.Order(StringComparer.Ordinal))}
            {string.Join("\n", tools)}

            Every tool call is audited. Argument names must match exactly. Unknown arguments are rejected.
            """;
    }
}
