# Conversion, publish and load safety fixes

These fixes came out of moving existing Text columns to Reference and Color columns in a project that already had data in them. Each one removes a case where Schema reported success, or only logged a line, while data was lost, changed meaning, or went missing. The numbers are the numbers of the findings in that project's report, kept here so the two lists can be matched; the last section maps each one to its commit.

Every entry has the same three parts: what was wrong, what changed, and how you notice the change.

## 3, 4, 16: `ConvertAttributeType` is all-or-nothing, and says where it failed

**What was wrong.** Converting a column wrote each converted cell as it went. When a later cell could not convert, the call returned a failure but the cells before it stayed converted, the column kept its old type, and the scheme was not marked dirty, so a save later wrote the converted values into a column of the old type. The failure named the column and the data type but not the row or the value. Converting a column that does not exist threw a `NullReferenceException`.

**What changed.** `DataScheme.ConvertAttributeType` converts every cell into a buffer first and writes only if all of them converted. On failure the cells, the column type, the column default and the dirty state are exactly as they were. The failure message names the scheme, the column, and each row that failed, with its identifier (or its position, counting from 1, when the scheme has no identifier column) and the value, up to ten rows and then a count. Converting an unknown column, or converting to a `null` type, returns a failed result.

**How you notice.** A conversion either happens completely or not at all. The message reads like `Cannot convert attribute Score of scheme 'Items' to type DataType: Integer, 2 of 5 rows failed: row 2 ('Item_2') value 'oops': ...`.
