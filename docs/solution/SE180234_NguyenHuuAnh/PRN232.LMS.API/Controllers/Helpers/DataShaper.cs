using System.Dynamic;
using System.Reflection;

namespace PRN232.LMS.API.Controllers.Helpers;

public static class DataShaper
{
    /// <summary>
    /// Shapes data by returning only the requested fields.
    /// </summary>
    public static IEnumerable<ExpandoObject> ShapeData<T>(IEnumerable<T> entities, string? fieldsString)
    {
        var propertyInfoList = GetPropertyInfos<T>(fieldsString);
        var shapedData = new List<ExpandoObject>();

        foreach (var entity in entities)
        {
            var shapedObject = FetchDataForEntity(entity, propertyInfoList);
            shapedData.Add(shapedObject);
        }

        return shapedData;
    }

    /// <summary>
    /// Shapes a single entity.
    /// </summary>
    public static ExpandoObject ShapeData<T>(T entity, string? fieldsString)
    {
        var propertyInfoList = GetPropertyInfos<T>(fieldsString);
        return FetchDataForEntity(entity, propertyInfoList);
    }

    private static IEnumerable<PropertyInfo> GetPropertyInfos<T>(string? fieldsString)
    {
        var propertyInfos = new List<PropertyInfo>();

        if (string.IsNullOrWhiteSpace(fieldsString))
        {
            // If no fields specified, return all public properties
            propertyInfos.AddRange(typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }
        else
        {
            var fields = fieldsString.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var field in fields)
            {
                var propertyName = field.Trim();
                var propertyInfo = typeof(T).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                if (propertyInfo != null)
                {
                    propertyInfos.Add(propertyInfo);
                }
            }
        }

        return propertyInfos;
    }

    private static ExpandoObject FetchDataForEntity<T>(T entity, IEnumerable<PropertyInfo> propertyInfos)
    {
        var shapedObject = new ExpandoObject();

        foreach (var propertyInfo in propertyInfos)
        {
            var objectPropertyValue = propertyInfo.GetValue(entity);
            // Convert to camelCase
            var propertyName = propertyInfo.Name;
            var camelCaseName = string.IsNullOrEmpty(propertyName) 
                ? propertyName 
                : char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
                
            ((IDictionary<string, object?>)shapedObject).Add(camelCaseName, objectPropertyValue);
        }

        return shapedObject;
    }
}
