using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IProductRepository
{
    Product CreateProduct(Product product);
    Product? GetProduct(Guid id);
    List<Product> GetProductsByBusinessProfile(Guid businessProfileId);
    Product? UpdateProduct(Product product);
    bool SoftDeleteProduct(Guid id);
    List<Product> GetUserProducts(Guid userId);
    (List<Product> Products, int Total) SearchProducts(ProductSearchDto searchDto);
}
