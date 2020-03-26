using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace MongoDB.UnitOfWork
{
    public interface IMongoDbSet<TEntity>
        where TEntity : class
    {
        TEntity Find(object id);

        List<TEntity> ToList();

        TEntity FirstOrDefault(Expression<Func<TEntity, bool>> predicate);

        IDocumentQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate);
    }
}
