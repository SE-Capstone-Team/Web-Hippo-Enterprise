# Hippo Exchange

## API Endpoints

### POST /api/users
Creates a new user profile.
- Body JSON: `firstName` (string), `lastName` (string), `email` (string), `address` (string), optional `ownerId` (string)
- Success: `201 Created` with the saved profile and `Location` header

### GET /api/users/{ownerId}
Retrieves a user profile by its identifier.
- Path parameter: `ownerId` (string)
- Success: `200 OK` with the profile
- Not found: `404 Not Found`

### PUT /api/users/{ownerId}
Replaces the stored profile for the specified identifier.
- Path parameter: `ownerId` (string)
- Body JSON: `firstName`, `lastName`, `email`, `address`, optional `ownerId` (string that should match the path)
- Success: `204 No Content`
- Not found: `404 Not Found`

### DELETE /api/users/{ownerId}
Deletes the profile for the given identifier.
- Path parameter: `ownerId` (string)
- Success: `204 No Content`
- Not found: `404 Not Found`

### POST /api/items
Creates a new inventory item.
- Body JSON: `name` (string), `pricePerDay` (number), `picture` (string), `location` (string), `condition` (string), `isLent` (bool), `ownerId` (string), optional `itemId` (string)
- Success: `201 Created` with the saved item and `Location` header

### GET /api/items/{itemId}
Retrieves an inventory item by its identifier.
- Path parameter: `itemId` (string)
- Success: `200 OK` with the item
- Not found: `404 Not Found`

### PUT /api/items/{itemId}
Replaces the stored item data for the specified identifier.
- Path parameter: `itemId` (string)
- Body JSON: `name`, `pricePerDay`, `picture`, `location`, `condition`, `isLent`, `ownerId`, optional `itemId` (string that should match the path)
- Success: `204 No Content`
- Not found: `404 Not Found`

### DELETE /api/items/{itemId}
Removes the inventory item for the given identifier.
- Path parameter: `itemId` (string)
- Success: `204 No Content`
- Not found: `404 Not Found`