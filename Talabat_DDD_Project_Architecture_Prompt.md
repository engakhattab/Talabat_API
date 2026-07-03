# Role

You are a Principal Software Architect, Domain-Driven Design (DDD)
expert, and Senior .NET Backend Engineer with extensive experience
designing scalable food delivery systems similar to Talabat, Uber Eats,
and DoorDash.

Your task is **NOT** to generate code immediately.

Instead, your task is to act as my software architect and mentor,
helping me design the project correctly before implementation.

# Project Context

I am building a **Talabat-like backend system** as a personal learning
project.

The purpose of this project is **learning**, not building a
production-ready enterprise system.

## Goals

-   Learn Domain-Driven Design (DDD) properly.
-   Learn Clean Architecture.
-   Learn ASP.NET Core.
-   Learn EF Core Code First.
-   Learn how business requirements become a domain model.
-   Learn how to design scalable software from the beginning.
-   Avoid over-engineering.
-   Build a solid MVP that can later evolve naturally into a larger
    system.

Whenever there is a trade-off between simplicity and enterprise
complexity, choose the **simpler solution** unless the simplification
would teach me the wrong architectural concept.

# Technology Stack

-   ASP.NET Core Web API
-   C#
-   EF Core (Code First)
-   SQL Server
-   ASP.NET Core Identity
-   Clean Architecture
-   Domain-Driven Design

# MVP Scope

## Catalog

-   Restaurants
-   Products

## Customer

-   Customer Profile
-   Customer Addresses

## Cart

-   Cart
-   Cart Items

## Ordering

-   Orders
-   Order Items

Authentication will use ASP.NET Core Identity.

Excluded from MVP:

-   Payment Gateway
-   Delivery Drivers
-   Notifications
-   Discounts
-   Coupons
-   Offers
-   Product Options
-   Product Variants
-   Restaurant Branches
-   Multi-currency
-   Inventory Management

These features will be added later.

# Business Requirements

## Restaurants

-   Customers can browse restaurants.
-   Restaurants have products.
-   Restaurants have opening hours.
-   MVP uses `OpensAt` and `ClosesAt`.
-   Restaurant may become unavailable or inactive.
-   Checkout must fail if the restaurant is closed.

## Products

Each product belongs to exactly one restaurant.

Fields:

-   Name
-   Description
-   Current Price
-   Availability

MVP supports only one price per product.

## Customer

-   Normal customers only.
-   One active cart only.
-   Multiple saved addresses.

## Cart Rules

-   One active cart per customer.
-   Cart belongs to one restaurant.
-   Cart contains many items.
-   CartItem references one Product.
-   Products from different restaurants are not allowed.
-   Reject adding products from another restaurant.
-   Cart expires after **1 hour**.
-   Expired carts cannot be modified.

## Quantity Rules

-   Quantity \> 0.
-   No negative quantities.
-   Duplicate products increase quantity instead of inserting another
    row.

## Pricing Rules

Product prices may change.

When adding to cart:

Store a temporary price snapshot:

-   ProductId
-   Quantity
-   UnitPriceSnapshot

Before checkout validate:

-   Current product prices
-   Product availability
-   Restaurant availability
-   Restaurant opening hours

If any price changes:

-   Reject checkout.
-   Return changed items.
-   Customer must accept new prices.
-   Update the cart snapshot after acceptance.

## Order Rules

Successful checkout creates an Order.

OrderItems store immutable snapshots:

-   Product snapshot
-   Unit price
-   Quantity
-   Line total

Changing Product.Price later must never affect previous orders.

# Domain Design

## Catalog

Aggregate Root:

-   Restaurant

Contains:

-   Product

## Cart

Aggregate Root:

-   Cart

Contains:

-   CartItem

## Ordering

Aggregate Root:

-   Order

Contains:

-   OrderItem

## Customer

Aggregate Root:

-   Customer

Contains:

-   CustomerAddress

# Database

Entities:

-   Customer
-   CustomerAddress
-   Restaurant
-   Product
-   Cart
-   CartItem
-   Order
-   OrderItem

Database:

-   SQL Server
-   EF Core Code First
-   Integer Identity primary keys

# Business Invariants

Must be enforced inside the Domain layer.

1.  One active cart per customer.
2.  One cart belongs to one restaurant.
3.  All CartItems belong to the same restaurant.
4.  Quantity \> 0.
5.  Duplicate products increase quantity.
6.  Expired carts cannot be modified.
7.  Checkout validates current prices.
8.  Orders store immutable prices.

# What I Need

Act as my software architect.

Do **not** skip design steps.

Guide me exactly like a senior architect mentoring a junior developer.

For every step explain:

-   Why we do it.
-   What problem it solves.
-   What I should learn.
-   Common beginner mistakes.
-   What to implement now.
-   What to postpone.

Prefer practical DDD over theoretical DDD.

# Expected Roadmap

Generate a detailed implementation roadmap including:

1.  Validate requirements
2.  Review bounded contexts
3.  Review aggregates
4.  Design entities
5.  Design value objects
6.  Design domain methods
7.  Design domain exceptions
8.  Design repository interfaces
9.  Design application use cases
10. Commands & Queries
11. Project structure
12. Domain layer
13. Infrastructure layer
14. EF Core configuration
15. Database migrations
16. API endpoints
17. Testing strategy
18. Future improvements

For every step include:

-   Objective
-   Deliverables
-   Implementation order
-   DDD reasoning
-   Clean Architecture reasoning
-   Things to avoid
-   Estimated difficulty
-   Learning outcome

Produce the roadmap as a professional software architecture document
that can guide the entire project.
