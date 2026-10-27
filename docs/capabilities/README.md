# Capabilities

What NCode.Identity does, what it promises, and why it's built the way it is.

Each page here covers one capability. The top half is written for anyone evaluating the product: the promises, the
reasons they matter, and how the design compares with the usual alternatives. The bottom half is written for engineers
and operators: how the capability works and where its limits are. Each page links to the
[ADRs](../decisions/README.md) that record the decisions and to the code that implements them, and those stay the source of
truth.

| Capability                                       | In one line                                                                                                         |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| 🔐 [Encryption at Rest](./encryption-at-rest.md) | Every stored secret is encrypted under a root key held in a KMS. Tenants are isolated and can be crypto-shredded, and secrets are wiped from memory after use. |

## Adding a capability

Copy [`_template.md`](./_template.md), keep its section order, and add a row to the table above. Lead with what the
reader gets, not with how it's built. Keep the top of the page free of type names, and link to an ADR rather than
restating its reasoning. Promises must be true of the code today: anything not built yet goes under **Limits and
roadmap**, with its issue.
