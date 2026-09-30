# 🔌 MCP ERP Server

![Status](https://img.shields.io/badge/status-in%20progress-orange?style=for-the-badge) ![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white) ![MCP](https://img.shields.io/badge/MCP-000000?style=for-the-badge&logo=anthropic&logoColor=white) ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-336791?style=for-the-badge&logo=postgresql&logoColor=white)

> A Model Context Protocol (MCP) server that lets AI agents safely query and act on ERP data: orders, stock and customers.

## 🎯 Why this project

AI agents are only useful when they can reach real business systems. This server exposes a sample ERP as well-defined MCP tools, with read-only defaults, input validation and audit logging, so an agent can answer questions like "what's the stock level of this product?" or draft an order for human approval.

## 🧱 Planned stack

- .NET 8 and C#
- Model Context Protocol server SDK
- PostgreSQL with a sample wholesale ERP schema
- Structured logging and audit trail
- Docker Compose for local setup

## 🗺️ Roadmap

- [ ] Sample ERP schema and seed data
- [ ] Read tools: customers, products, stock, orders
- [ ] Draft-order tool that requires human approval
- [ ] Input validation and permission scopes
- [ ] Audit log of every tool call
- [ ] Guide for connecting to MCP clients

## 📌 Status

🚧 This project is in early development. Code is coming soon. It uses synthetic sample data only.

---

Built by [Jahanzaib Ahmad](https://github.com/jehanxaibahmed) · Full Stack Engineer · AI & LLM Systems
