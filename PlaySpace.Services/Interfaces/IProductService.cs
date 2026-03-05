using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IProductService
{
    ProductResponseDto CreateProduct(Guid businessProfileId, CreateProductDto dto, Guid userId);
    ProductResponseDto? GetProduct(Guid productId);
    List<ProductResponseDto> GetProductsByBusinessProfile(Guid businessProfileId);
    ProductResponseDto? UpdateProduct(Guid businessProfileId, Guid productId, UpdateProductDto dto, Guid userId);
    bool DeleteProduct(Guid businessProfileId, Guid productId, Guid userId);
    List<ProductResponseDto> GetMyProducts(Guid userId);
    ProductSearchResponseDto SearchProducts(ProductSearchDto searchDto);
}
