# Talabat MVP v1 - Business Rules

MVP v1 does not include authentication or authorization. All API actions assume a single normal customer. Catalog data will be seeded for testing.

## 1. Catalog Rules

### BR-CAT-001 - Only active restaurants are visible to customers

Given a restaurant is inactive,  
When the customer browses restaurants,  
Then the restaurant should not appear in the result.

### BR-CAT-002 - A product belongs to exactly one restaurant

Given a product is created,  
When it is saved,  
Then it must be linked to one restaurant.

### BR-CAT-003 - Customers can only see available products

Given a product is unavailable,  
When the customer views a restaurant menu,  
Then the product should not appear as available for ordering.

### BR-CAT-004 - Product price cannot be negative

Given catalog data is seeded or updated for testing,  
When the price is less than zero,  
Then the system rejects the action.

### BR-CAT-005 - Restaurant must have valid opening hours

Given catalog data is seeded or updated for testing,  
When opening hours are invalid,  
Then the system rejects the restaurant data.

---

## 2. Basket / Cart Rules

### BR-CART-001 - Customer can have only one active cart

Given the customer already has an active cart,  
When they add another product,  
Then the item should be added to the existing active cart.

### BR-CART-002 - Cart expires after 1 hour

Given a cart was created more than 1 hour ago,  
When the customer tries to modify it,  
Then the system rejects the action with CartExpiredException.

### BR-CART-003 - Expired cart cannot be checked out

Given a cart is expired,  
When the customer tries to checkout,  
Then the system rejects the checkout with CartExpiredException.

### BR-CART-004 - Cart can contain items from only one restaurant

Given a cart contains items from Restaurant A,  
When the customer tries to add a product from Restaurant B,  
Then the system rejects the action with CrossRestaurantCartException.

### BR-CART-005 - Quantity must be greater than zero

Given the customer adds or updates a cart item,  
When the quantity is zero or negative,  
Then the system rejects the action with InvalidQuantityException.

### BR-CART-006 - Duplicate products are merged

Given a cart already contains Product A with quantity 2,  
When the customer adds Product A again with quantity 3,  
Then the cart should contain Product A with quantity 5.

### BR-CART-007 - Product price is snapshotted in cart

Given the customer adds a product to the cart,  
When the product is added,  
Then the cart item stores the product name and unit price at that time.

### BR-CART-008 - Unavailable products cannot be added to cart

Given a product is unavailable,  
When the customer tries to add it to the cart,  
Then the system rejects the action with ProductUnavailableException.

### BR-CART-009 - Customer can clear cart

Given the customer has an active cart,  
When the customer clears the cart,  
Then all cart items are removed.

---

## 3. Ordering / Checkout Rules

### BR-ORD-001 - Empty cart cannot be checked out

Given the customer has an empty cart,  
When they try to checkout,  
Then the system rejects the checkout with EmptyCartCheckoutException.

### BR-ORD-002 - Restaurant must be active during checkout

Given the cart restaurant is inactive,  
When the customer tries to checkout,  
Then the system rejects the checkout with RestaurantInactiveException.

### BR-ORD-003 - Restaurant must be open during checkout

Given the restaurant is closed at checkout time,  
When the customer tries to checkout,  
Then the system rejects the checkout with RestaurantClosedException.

### BR-ORD-004 - Checkout validates product availability again

Given a product was available when added to cart but is now unavailable,  
When the customer tries to checkout,  
Then the system rejects the checkout and returns the unavailable item.

### BR-ORD-005 - Checkout validates current prices

Given a product price changed after it was added to cart,  
When the customer tries to checkout,  
Then the system rejects the checkout and returns the changed item.

### BR-ORD-006 - Order stores immutable item snapshots

Given checkout succeeds,  
When the order is created,  
Then each order item stores product id, product name, unit price, quantity, and line total.

### BR-ORD-007 - Order total is calculated from order items

Given an order is created,  
When the system calculates total amount,  
Then total amount equals the sum of all order item line totals.

### BR-ORD-008 - Customer can only view their own orders

Given the customer profile owns an order,  
When orders are requested,  
Then the system returns only orders linked to that customer profile.

---

## 4. Customer Profile Rules

### BR-CUS-001 - Customer profile exists before using customer features

Given the system has a customer profile,  
When the customer uses cart, address, or order features,  
Then those records are linked to that customer profile.

### BR-CUS-002 - Customer can have multiple addresses

Given a customer profile exists,  
When the customer adds addresses,  
Then the system allows multiple addresses.

### BR-CUS-003 - Customer can have only one default address

Given a customer has multiple addresses,  
When one address is set as default,  
Then all other addresses must become non-default.

### BR-CUS-004 - Duplicate address should be rejected

Given the customer already has an address,  
When they add the exact same address again,  
Then the system rejects it with DuplicateAddressException.

### BR-CUS-005 - Checkout requires a delivery address

Given the customer tries to checkout,  
When no delivery address is selected,  
Then the system rejects the checkout.
