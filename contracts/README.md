# contracts/

Vendored, read-only copies of this service's approved design artifacts from
`kart-platform/docs/services/kart-inventory-service/`:

- `api-contract.yaml` - the REST + internal gRPC contract (with one addendum documented in its
  own header - `POST /inventory/replenish`, which the upstream artifact never actually defined a
  path for despite requiring it).
- `message-bus-manifest.json` - the **single source of truth** for this service's entire RabbitMQ
  topology. `Infrastructure/Messaging/RabbitMqTopologyProvisioner` declares every exchange, queue,
  binding, dead-letter queue, and retry-tier queue directly from this file at startup - nothing
  messaging-related is a hardcoded string literal in C#. This mirrors `kart-identity-service` and
  `kart-category-service` exactly.

Update either file only by re-copying the upstream file (or, for the `api-contract.yaml`
addendum, by updating the upstream artifact itself and re-copying) - never by editing the copy
here directly out of sync with its source of truth.
