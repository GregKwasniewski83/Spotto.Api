using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly PlaySpaceDbContext _context;

    public ProductRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public Product CreateProduct(Product product)
    {
        _context.Products.Add(product);
        _context.SaveChanges();
        return product;
    }

    public Product? GetProduct(Guid id)
    {
        return _context.Products
            .Include(p => p.BusinessProfile)
                .ThenInclude(bp => bp!.ParentBusinessProfile)
            .Include(p => p.User)
            .FirstOrDefault(p => p.Id == id && p.IsActive);
    }

    public List<Product> GetProductsByBusinessProfile(Guid businessProfileId)
    {
        return _context.Products
            .Include(p => p.BusinessProfile)
            .Include(p => p.User)
            .Where(p => p.BusinessProfileId == businessProfileId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
    }

    public Product? UpdateProduct(Product product)
    {
        var existingProduct = _context.Products.FirstOrDefault(p => p.Id == product.Id);
        if (existingProduct == null)
            return null;

        existingProduct.Title = product.Title;
        existingProduct.Subtitle = product.Subtitle;
        existingProduct.Description = product.Description;
        existingProduct.Price = product.Price;
        existingProduct.Usage = product.Usage;
        existingProduct.Period = product.Period;
        existingProduct.NumOfPeriods = product.NumOfPeriods;
        existingProduct.PayableInApp = product.PayableInApp;
        existingProduct.StartDate = product.StartDate;
        existingProduct.EndDate = product.EndDate;
        existingProduct.UpdatedAt = DateTime.UtcNow;

        _context.SaveChanges();
        return existingProduct;
    }

    public bool SoftDeleteProduct(Guid id)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
            return false;

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        _context.SaveChanges();
        return true;
    }

    public List<Product> GetUserProducts(Guid userId)
    {
        return _context.Products
            .Include(p => p.BusinessProfile)
            .Include(p => p.User)
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
    }

    public (List<Product> Products, int Total) SearchProducts(ProductSearchDto searchDto)
    {
        var query = _context.Products
            .Include(p => p.BusinessProfile)
            .Include(p => p.User)
            .Where(p => p.IsActive)
            .AsQueryable();

        // Search in title, subtitle, description, business name, and business city
        if (!string.IsNullOrWhiteSpace(searchDto.Search))
        {
            var searchLower = searchDto.Search.ToLower();
            query = query.Where(p =>
                p.Title.ToLower().Contains(searchLower) ||
                (p.Subtitle != null && p.Subtitle.ToLower().Contains(searchLower)) ||
                p.Description.ToLower().Contains(searchLower) ||
                (p.BusinessProfile != null && p.BusinessProfile.DisplayName != null && p.BusinessProfile.DisplayName.ToLower().Contains(searchLower)) ||
                (p.BusinessProfile != null && p.BusinessProfile.CompanyName.ToLower().Contains(searchLower)) ||
                (p.BusinessProfile != null && p.BusinessProfile.City != null && p.BusinessProfile.City.ToLower().Contains(searchLower)));
        }

        // Filter by business name
        if (!string.IsNullOrWhiteSpace(searchDto.BusinessName))
        {
            var businessNameLower = searchDto.BusinessName.ToLower();
            query = query.Where(p =>
                p.BusinessProfile != null &&
                ((p.BusinessProfile.DisplayName != null && p.BusinessProfile.DisplayName.ToLower().Contains(businessNameLower)) ||
                 p.BusinessProfile.CompanyName.ToLower().Contains(businessNameLower)));
        }

        // Filter by city
        if (!string.IsNullOrWhiteSpace(searchDto.City))
        {
            var cityLower = searchDto.City.ToLower();
            query = query.Where(p =>
                p.BusinessProfile != null &&
                p.BusinessProfile.City != null &&
                p.BusinessProfile.City.ToLower().Contains(cityLower));
        }

        // Filter by price range
        if (searchDto.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= searchDto.MinPrice.Value);
        }
        if (searchDto.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= searchDto.MaxPrice.Value);
        }

        // Filter by period
        if (!string.IsNullOrWhiteSpace(searchDto.Period))
        {
            if (Enum.TryParse<ProductPeriod>(searchDto.Period, true, out var period))
            {
                query = query.Where(p => p.Period == period);
            }
        }

        // Filter by usage range
        if (searchDto.MinUsage.HasValue)
        {
            query = query.Where(p => p.Usage >= searchDto.MinUsage.Value);
        }
        if (searchDto.MaxUsage.HasValue)
        {
            query = query.Where(p => p.Usage <= searchDto.MaxUsage.Value);
        }

        // Get total count before pagination
        var total = query.Count();

        // Sorting
        var sortBy = searchDto.SortBy?.ToLower() ?? "createdat";
        var sortOrder = searchDto.SortOrder?.ToLower() ?? "desc";

        query = sortBy switch
        {
            "price" => sortOrder == "desc" ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "title" => sortOrder == "desc" ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),
            "usage" => sortOrder == "desc" ? query.OrderByDescending(p => p.Usage) : query.OrderBy(p => p.Usage),
            "createdat" => sortOrder == "desc" ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        // Pagination
        var limit = searchDto.Limit ?? 20;
        if (limit > 100) limit = 100;  // Max limit
        if (limit < 1) limit = 20;

        int skip;
        if (searchDto.Offset.HasValue)
        {
            skip = searchDto.Offset.Value;
        }
        else
        {
            var page = searchDto.Page ?? 1;
            if (page < 1) page = 1;
            skip = (page - 1) * limit;
        }

        var products = query
            .Skip(skip)
            .Take(limit)
            .ToList();

        return (products, total);
    }
}
