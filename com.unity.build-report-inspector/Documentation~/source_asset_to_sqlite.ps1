# Demonstation powershell script that can import the CVS output from the
# BuildReportInspector SourceAssets tab into a new sqlite3 database.
#
# This requires that sqlite3.exe is on your path, this is available in the
# SQLite command line tools published from www.sqlite.org.
#
# DISCLAIMER:
# This script is provided "as-is," without any warranty of any kind, express or implied.
# By using this script, you agree that you understand its purpose and that you use it entirely at your own risk.
# The author assumes no liability for any damages resulting from its use, misuse, or inability to use.
#
# Always review and test this script in a safe environment before applying it to a production system.

# Check for arguments
if ($Args.Count -ne 2) {
    Write-Host "Usage: script.ps1 <csv_file> <output_database>"
    Write-Host "Example: script.ps1 assets.csv content_entries.db"
    exit 1
}

# Arguments
$CSV_FILE = $Args[0]
$DB_FILE = $Args[1]
$TABLE_NAME = "ContentEntry"

# Check if the CSV file exists
if (-Not (Test-Path $CSV_FILE)) {
    Write-Host "Error: CSV file '$CSV_FILE' does not exist."
    exit 1
}

# Remove existing SQLite database file (if it exists)
if (Test-Path $DB_FILE) {
    Write-Host "Removing existing database file '$DB_FILE'..."
    Remove-Item $DB_FILE -Force

	if (Test-Path $DB_FILE) {
		Write-Host "Exiting script. Failed to remove existing file '$DB_FILE'."
		exit 1
	}
}


# Create SQLite database and table
Write-Host "Creating SQLite database '$DB_FILE' and table '$TABLE_NAME'..."
& sqlite3 $DB_FILE @"
CREATE TABLE $TABLE_NAME (
    SourceAssetPath TEXT,
    OutputFile TEXT,
    Type TEXT,
    Size INTEGER,
    ObjectCount INTEGER,
	Extension TEXT,
	AssetBundlePath TEXT
);
"@

# Skip header (first line) and prepare a temporary CSV file
$tempCsv = "$env:TEMP\temp_import.csv"
Write-Host "Preparing a temporary CSV file without the header..."
Get-Content $CSV_FILE | Select-Object -Skip 1 | Set-Content $tempCsv

# Import CSV data from temporary file into SQLite
Write-Host "Importing data from temporary file into table '$TABLE_NAME'..."
& sqlite3 $DB_FILE ".mode csv" ".import $tempCsv $TABLE_NAME"

# Cleanup: Remove temporary file
Write-Host "Cleaning up temporary file..."
Remove-Item $tempCsv -Force

Write-Host "Import Complete. Querying the count of rows in '$TABLE_NAME':"
& sqlite3 $DB_FILE "SELECT COUNT(*) FROM $TABLE_NAME;"

Write-Host "Script completed successfully! Data was imported into '$DB_FILE'."
