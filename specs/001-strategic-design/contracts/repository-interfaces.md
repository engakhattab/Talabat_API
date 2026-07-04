# Repository Interface Contracts

These interfaces define the data access contracts that the Domain layer demands from the Infrastructure layer. They act as Anti-Corruption boundaries.

## IRestaurantRepository
```csharp
public interface IRestaurantRepository
{
    Task<Restaurant?> GetByIdAsync(int id);
    Task<IReadOnlyList<Restaurant>> GetAllAsync(bool includeInactive = false);
    Task AddAsync(Restaurant restaurant);
    void Update(Restaurant restaurant);
}
```

## ICartRepository
```csharp
public interface ICartRepository
{
    Task<Cart?> GetActiveCartByCustomerIdAsync(int customerId);
    Task AddAsync(Cart cart);
    void Update(Cart cart);
    Task DeleteAsync(Cart cart); // For explicitly clearing carts
}
```

## IOrderRepository
```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(int customerId);
    Task AddAsync(Order order);
}
```

## ICustomerRepository
```csharp
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id);
    Task AddAsync(Customer customer);
    void Update(Customer customer);
}
```

## IUnitOfWork
```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```
