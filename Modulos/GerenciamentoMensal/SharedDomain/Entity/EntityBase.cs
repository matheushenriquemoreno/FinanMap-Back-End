using SharedDomain.Entity;

namespace Domain.Entity;

public abstract class EntityBase : IEntityBase
{
    public virtual string Id { get; set; } = string.Empty;
}
