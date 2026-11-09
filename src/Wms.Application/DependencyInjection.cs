using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wms.Application.Features.Master.Categories;
using Wms.Application.Features.Master.Customers;
using Wms.Application.Features.Master.Products;
using Wms.Application.Features.Master.Suppliers;
using Wms.Application.Features.Master.Units;
using Wms.Application.Features.Master.Warehouses;

namespace Wms.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Master Data Application Services
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ICustomerService, CustomerService>();

        return services;
    }
}
