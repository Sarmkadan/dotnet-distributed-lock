# LockEventSubscriberValidation

Provides static validation utilities for lock event subscriber implementations, ensuring they conform to the required contracts before registration with the distributed lock subsystem. The class offers three complementary validation approaches: collecting all violations, boolean predicate checks, and guard-clause enforcement that throws on failure.

## API

### `Validate(ILockEventSubscriber subscriber)`
Validates a single lock event subscriber instance.

**Parameters**
- `subscriber` — The subscriber implementation to validate. Must not be null.

**Returns**
An `IReadOnlyList<string>` containing zero or more validation error messages. An empty list indicates the subscriber is valid.

**Throws**
- `ArgumentNullException` — If `subscriber` is null.

---

### `Validate(IEnumerable<ILockEventSubscriber> subscribers)`
Validates a collection of lock event subscriber instances.

**Parameters**
- `subscribers` — The subscribers to validate. The collection itself must not be null, and must not contain null elements.

**Returns**
An `IReadOnlyList<string>` containing aggregated validation error messages across all subscribers. An empty list indicates all subscribers are valid.

**Throws**
- `ArgumentNullException` — If `subscribers` is null or contains a null element.

---

### `Validate(LockEventSubscriberRegistration registration)`
Validates a subscriber registration descriptor that bundles a subscriber with its filter criteria.

**Parameters**
- `registration` — The registration object describing the subscriber and its event filters. Must not be null.

**Returns**
An `IReadOnlyList<string>` containing validation error messages. An empty list indicates the registration is valid.

**Throws**
- `ArgumentNullException` — If `registration` is null.

---

### `IsValid(ILockEventSubscriber subscriber)`
Determines whether a single subscriber satisfies all validation rules.

**Parameters**
- `subscriber` — The subscriber to check. Must not be null.

**Returns**
`true` if the subscriber is valid; otherwise `false`.

**Throws**
- `ArgumentNullException` — If `subscriber` is null.

---

### `IsValid(IEnumerable<ILockEventSubscriber> subscribers)`
Determines whether all subscribers in a collection are valid.

**Parameters**
- `subscribers` — The subscribers to check. Must not be null and must not contain null elements.

**Returns**
`true` if every subscriber is valid; otherwise `false`.

**Throws**
- `ArgumentNullException` — If `subscribers` is null or contains a null element.

---

### `IsValid(LockEventSubscriberRegistration registration)`
Determines whether a subscriber registration descriptor is valid.

**Parameters**
- `registration` — The registration to check. Must not be null.

**Returns**
`true` if the registration is valid; otherwise `false`.

**Throws**
- `ArgumentNullException` — If `registration` is null.

---

### `EnsureValid(ILockEventSubscriber subscriber)`
Throws an exception if the subscriber fails validation; otherwise returns normally.

**Parameters**
- `subscriber` — The subscriber to validate. Must not be null.

**Throws**
- `ArgumentNullException` — If `subscriber` is null.
- `LockEventSubscriberValidationException` — If validation fails. The exception contains the aggregated error messages.

---

### `EnsureValid(IEnumerable<ILockEventSubscriber> subscribers)`
Throws an exception if any subscriber in the collection fails validation.

**Parameters**
- `subscribers` — The subscribers to validate. Must not be null and must not contain null elements.

**Throws**
- `ArgumentNullException` — If `subscribers` is null or contains a null element.
- `LockEventSubscriberValidationException` — If one or more subscribers are invalid. The exception contains all aggregated error messages.

---

### `EnsureValid(LockEventSubscriberRegistration registration)`
Throws an exception if the registration descriptor fails validation.

**Parameters**
- `registration` — The registration to validate. Must not be null.

**Throws**
- `ArgumentNullException` — If `registration` is null.
- `LockEventSubscriberValidationException` — If validation fails. The exception contains the aggregated error messages.

## Usage

### Example 1: Guard-clause validation during registration
```csharp
var subscriber = new DatabaseLockEventSubscriber(connectionString);
var registration = new LockEventSubscriberRegistration(subscriber, LockEventTypes.Acquired | LockEventTypes.Released);

LockEventSubscriberValidation.EnsureValid(registration);

await lockManager.SubscribeAsync(registration);
```

### Example 2: Collecting validation errors for diagnostic logging
```csharp
var subscribers = new[]
{
    new MetricsLockEventSubscriber(metrics),
    new AuditLockEventSubscriber(auditLog),
    new NotificationLockEventSubscriber(notifier)
};

var errors = LockEventSubscriberValidation.Validate(subscribers);

if (errors.Count > 0)
{
    logger.LogWarning("Lock event subscriber validation failed: {Errors}", string.Join("; ", errors));
    return;
}

foreach (var s in subscribers)
{
    await lockManager.SubscribeAsync(s);
}
```

## Notes

- **Thread safety**: All methods are pure static functions with no shared mutable state. They are safe for concurrent invocation from multiple threads.
- **Null handling**: Every overload throws `ArgumentNullException` for null inputs (including null elements within collections). Callers should ensure non-null arguments before invoking, or catch the exception.
- **Validation scope**: Checks include but are not limited to: required interface implementation, non-null handler delegates, valid event filter combinations, and absence of duplicate subscriptions for the same event type within a single registration.
- **Exception type**: `EnsureValid` throws `LockEventSubscriberValidationException`, a dedicated exception type that exposes the full list of validation messages via its `Errors` property, enabling programmatic inspection without parsing the message string.
- **Performance**: `Validate` allocates a list for the error messages; `IsValid` short-circuits on the first failure and avoids allocation when valid. Prefer `IsValid` for hot paths where only a boolean result is needed.
- **Idempotence**: Validation is deterministic and side-effect-free. Repeated calls with the same arguments yield identical results.
