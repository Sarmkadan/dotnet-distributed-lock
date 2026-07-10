# LockNotOwnedException

Exception thrown when an attempt is made to release or manipulate a distributed lock that is not owned by the current process or thread.

## API

### `public string LockKey`
Gets the key of the lock that was attempted to be released or manipulated.

### `public string OwnerId`
Gets the unique identifier of the process or thread that currently owns the lock.

### `public string ProvidedOwnerId`
Gets the owner identifier that was provided in the operation that triggered this exception.

### `public LockNotOwnedException()`
Initializes a new instance of the `LockNotOwnedException` class with default values.

### `public LockNotOwnedException(string message)`
Initializes a new instance of the `LockNotOwnedException` class with a specified error message.

## Usage
