# Hướng dẫn sử dụng class DatabaseAction

Class `DatabaseAction` nằm trong namespace `CourseGuard.Data`, cung cấp các phương thức tĩnh (static methods) để thao tác với cơ sở dữ liệu SQL Server một cách an toàn và tiện lợi.

## Các phương thức chính

### 1. ExecuteNonQuery

Dùng cho các câu lệnh **INSERT, UPDATE, DELETE**.

**Cú pháp:**
```csharp
public static int ExecuteNonQuery(string query, Dictionary<string, (SqlDbType Type, object Value)> parameters = null)
```

**Trả về:** `int` - Số dòng bị ảnh hưởng bởi câu lệnh.

**Ví dụ:**
```csharp
string query = "INSERT INTO USERS (USERNAME, FULL_NAME) VALUES (@user, @name)";
var parameters = new Dictionary<string, (SqlDbType, object)>
{
    { "@user", (SqlDbType.NVarChar, "nguyenvana") },
    { "@name", (SqlDbType.NVarChar, "Nguyen Van A") }
};
int result = DatabaseAction.ExecuteNonQuery(query, parameters);
```

### 2. ExecuteQuery

Dùng cho các câu lệnh **SELECT** để lấy dữ liệu dạng bảng.

**Cú pháp:**
```csharp
public static DataTable ExecuteQuery(string query, Dictionary<string, (SqlDbType Type, object Value)> parameters = null)
```

**Trả về:** `DataTable` - Bảng dữ liệu kết quả.

**Ví dụ:**
```csharp
string query = "SELECT * FROM USERS WHERE ROLE_ID = @role";
var parameters = new Dictionary<string, (SqlDbType, object)>
{
    { "@role", (SqlDbType.Int, 2) }
};
DataTable dt = DatabaseAction.ExecuteQuery(query, parameters);
dataGridView1.DataSource = dt;
```

### 3. ExecuteScalar

Dùng để lấy **một giá trị duy nhất** từ cơ sở dữ liệu (ví dụ: `COUNT`, `MAX`, `SUM` hoặc lấy ID vừa insert).

**Cú pháp:**
```csharp
public static object ExecuteScalar(string query, Dictionary<string, (SqlDbType Type, object Value)> parameters = null)
```

**Trả về:** `object` - Giá trị đầu tiên của hàng đầu tiên trong kết quả query. Cần ép kiểu về kiểu dữ liệu mong muốn.

**Ví dụ:**
```csharp
string query = "SELECT COUNT(*) FROM USERS";
int count = Convert.ToInt32(DatabaseAction.ExecuteScalar(query));
```

## Lưu ý

- **Parameters**: Luôn sử dụng `parameters` để truyền giá trị vào câu query nhằm tránh lỗi SQL Injection.
- **Connection**: Class tự động quản lý việc mở và đóng kết nối (`SqlConnection`), bạn không cần quan tâm đến việc này.
