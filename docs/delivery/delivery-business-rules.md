# Delivery Business Rules

These rules define the Delivery Extension for MVP v2. They extend the original MVP v1 without changing the ownership boundaries of Catalog, Basket, Customer, or Ordering.

## Delivery Creation

### BR-DEL-001 - Delivery is created after successful checkout

Given checkout succeeds,  
When an order is created,  
Then a delivery task should be created for that order.

### BR-DEL-002 - Delivery starts pending assignment

Given a delivery is created,  
When its initial state is established,  
Then its status should be `PendingAssignment`.

## Assignment

### BR-DEL-003 - Only available agents can be assigned

Given a delivery agent is `Offline`, `Busy`, or `Suspended`,  
When assignment is attempted,  
Then the system rejects the assignment.

### BR-DEL-004 - Agent can have only one active delivery

Given an agent already has an active delivery,  
When another delivery is assigned,  
Then the system rejects the assignment.

### BR-DEL-005 - Delivery can only be assigned once

Given a delivery is already assigned,  
When assignment is attempted again,  
Then the system rejects it.

## Delivery Lifecycle

### BR-DEL-006 - Delivery cannot be picked up before assignment

Given delivery is not assigned,  
When pickup is attempted,  
Then the system rejects the action.

### BR-DEL-007 - Agent must arrive at restaurant before pickup

Given delivery is `Assigned`,  
When pickup is attempted before `ArrivedAtRestaurant`,  
Then the system rejects it.

### BR-DEL-008 - Delivery cannot be delivered before pickup and out-for-delivery

Given delivery is not `OutForDelivery`,  
When delivered is attempted,  
Then the system rejects it.

### BR-DEL-009 - Terminal delivery cannot be changed

Given delivery is `Delivered`, `Cancelled`, or `Failed`,  
When a lifecycle update is attempted,  
Then the system rejects it.

### BR-DEL-010 - Agent becomes busy after assignment

Given an available agent is assigned to a delivery,  
When assignment succeeds,  
Then agent status becomes `Busy`.

### BR-DEL-011 - Agent becomes available after delivery completion

Given a delivery is delivered,  
When completion succeeds for the assigned agent,  
Then agent status becomes `Available`.

## References And Snapshots

### BR-DEL-012 - Delivery stores delivery address snapshot

Given delivery is created,  
When its initial state is established,  
Then it stores the delivery address snapshot copied from the order.

### BR-DEL-013 - Delivery references related concepts by IDs

Given delivery belongs to an order,  
When the delivery is created or assigned,  
Then it stores `OrderId`, `CustomerId`, `RestaurantId`, and an optional `AssignedAgentId`.

Delivery does not contain navigation object references to Order, Customer, Restaurant, or DeliveryAgent in the Domain model.

## Agent Location

### BR-DEL-014 - Agent current location is optional

Given an agent is created,  
Then current location may be null.

Given an agent location is updated,  
When latitude or longitude is outside its valid range,  
Then the system rejects the location.

Valid latitude is between -90 and 90. Valid longitude is between -180 and 180.
