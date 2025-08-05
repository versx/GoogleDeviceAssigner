# 🧑‍🏫 Student ChromeOS Device Assigner

A powerful C# .NET console application to automate the assignment and management of Chrome OS devices using a CSV list and Google Workspace Admin SDK + Sheets API integration. Designed for schools or organizations managing Chromebooks in carts with minimal manual input.


## ✨ Features

- 🔄 **Bulk update Chrome OS devices** using a simple CSV.
- 📝 **Set Asset ID**, update **Notes**, and **move to Org Units** (OU).
- 📄 **Update or create Google Sheets** tabs per cart (e.g., `Cart 14`) with structured device data.
- 🛠️ Automatically create missing tabs and append or update rows based on `SerialNumber`.
- 💬 Supports **templated OU paths** (e.g. `/Chromebooks/Cart {CartNumber} {YearRange}`).
- 📂 Load config from `config.json` or pass via command line.
- ✅ Built-in error handling and dry-run support for testing.


## 🚀 Getting Started

### 1. **Install Dependencies**

Install [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download) or higher.

Install required NuGet packages:
```bash
dotnet restore
````


### 2. **Google Cloud Setup**

* Create a Google Cloud Project.
* Enable **Admin SDK** and **Sheets API**.
* Create a **Service Account** with **Domain-wide Delegation** enabled.
* Download the service account JSON file.
* Share your target Google Sheet with the service account email.
* Assign appropriate scopes:

  * `https://www.googleapis.com/auth/admin.directory.device.chromeos`
  * `https://www.googleapis.com/auth/admin.directory.orgunit`
  * `https://www.googleapis.com/auth/spreadsheets`


### 3. **Configuration File**

Create a `config.json` file or pass equivalent values as command-line arguments.

#### `config.json` example:

```json
{
  "cartNumber": 1,
  "ouTemplate": "/Chromebooks/Cart {DeviceNumber} {YearRange}",
  "csv": "devices.csv",
  "customerId": "my_customer",
  "adminUser": "superadmin@domain.com",
  "serviceAccount": "service-account.json",
  "googleSheetId": "1xYzABCDEF1234567890ghiJKLmnOpQrstuVWxyz",
  "promptOnError": true,
  "dryRun": false
}
```

#### Fields:

| Field                    | Description                                     |
| ------------------------ | ----------------------------------------------- |
| `CartNumber`             | Number to represent the cart.                   |
| `OrgUnitPathTemplate`    | Optional OU path template using placeholders.   |
| `CsvFilePath`            | Path to the device CSV.                         |
| `CustomerId`             | Usually `my_customer`.                          |
| `AdminUserToImpersonate` | Workspace admin email.                          |
| `ServiceAccountFilePath` | Path to downloaded service account credentials. |
| `GoogleSheetId`          | ID of the target Google Sheet to update.        |
| `PromptOnError`          | If true, waits for user input on errors.        |
| `IsDryRun`               | If true, no real changes will be made.          |


## 📥 Device CSV Format

The `devices.csv` contains the list of devices to update. Example:

```csv
DeviceNumber,SerialNumber,StudentName,PurchaseId,Damage
1,5CD123GDJ,John Smith,100-923,Scratches top lid
2,5CD123GDK,Jane Smith,100-923,
3,5CD123GDL,Petro Gonzalez,,
```

#### Column Explanation:

| Column         | Description                             |
| -------------- | --------------------------------------- |
| `DeviceNumber` | Number label on device (01-32).         |
| `SerialNumber` | Chromebook serial number. **Required**. |
| `StudentName`  | First and last name.                    |
| `PurchaseId`   | Purchase reference ID.                  |
| `Damage`       | Notes about device condition.           |


## 🧪 Running the Tool

Uses the `config.json` configuration file, if found in the current directory:
```bash
dotnet run
```

Or using command-line overrides (when config is missing or partial):

```bash
dotnet run -- \
  --cartNumber=23 \
  --ouTemplate="/Chromebooks/Cart {CartNumber} {YearRange}" \
  --csv="devices.csv" \
  --googleSheetId="1xYzABCDEF1234567890ghiJKLmnOpQrstuVWxyz" \
  --adminUser="admin@yourdomain.com" \
  --customerId="my_customer" \
  --serviceAccount="service-account.json"
```


## 🧠 Supported Template Placeholders

You can use placeholders in the Org Unit Path or any other future template string:

| Placeholder      | Description                         |
| ---------------- | ----------------------------------- |
| `{CartNumber}`   | Cart number (23)                    |
| `{SerialNumber}` | Device serial number                |
| `{DeviceNumber}` | Device number (01-32)               |
| `{PurchaseId}`   | Purchase ID                         |
| `{Year}`         | Current year (e.g., 2025)           |
| `{YearRange}`    | Academic year range (e.g., 2025-26) |

Example:

```json
"ouTemplate": "/Chromebooks/Cart {DeviceNumber} {YearRange}"
```

Resulting OU path: `/Chromebooks/Cart 01 2025-26`


## 📊 Google Sheet Structure

* Each cart is stored in a **tab named** like `Cart 14`, `Cart 15`, etc.
* The tool creates a new tab if it doesn't exist.
* Rows are updated by matching `SerialNumber`; new rows are appended otherwise.

#### Columns in Google Sheet:

```text
Device # | Serial Number | Cart # | MAC Address | Out of Service | Model | Student Name | Purchase ID | Damage
```


## 🧯 Troubleshooting

* **`unauthorized_client`**: Ensure you enabled **Domain-wide Delegation** and assigned proper OAuth scopes.
* **403 errors**: Confirm the service account has access to the Sheet and the Admin SDK.
* **Dry run mode**: Use `IsDryRun: true` to preview changes without affecting devices.


## 🛡️ License

MIT License. Free to use and modify.
