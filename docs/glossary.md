# Ubiquitous Language Glossary

- **Customer**: A user who uses the system to browse restaurants and place orders.
- **Restaurant**: A business entity that offers products for sale.
- **Product**: An item offered by a restaurant for purchase.
- **Cart**: A temporary container for products a customer intends to purchase.
- **Cart Item**: A specific product and its quantity within a cart.
- **Order**: A finalized, immutable record of a customer's purchase.
- **Order Item**: A specific product, quantity, and agreed price within an order.
- **Checkout**: The process of validating a cart and converting it into an order.
- **Aggregate**: A cluster of domain objects that can be treated as a single unit.
- **Aggregate Root**: The single entry point entity for an aggregate, responsible for ensuring the consistency of changes within the aggregate.
- **Entity**: An object defined primarily by its identity, rather than its attributes.
- **Value Object**: An object that contains attributes but has no conceptual identity.
- **Price Snapshot**: The price of a product at the time it was added to the cart, used for tracking changes.
- **Current Price**: The real-time price of a product as defined in the catalog.
- **Immutable Order Price**: The locked-in price of an item once an order is created.
- **Availability**: The state indicating whether a product or restaurant can currently accept orders.
- **Restaurant Opening Hours**: The specific time range during which a restaurant is open and accepting orders.
