using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace MongoDB.UnitOfWork
{
    public interface IMongoContext
    {
        IMongoDbSet<TEntity> Set<TEntity>() where TEntity : class;

        List<TEntity> All<TEntity>() where TEntity : class;

        List<TEntity> ToList<TEntity>(Expression<Func<TEntity, bool>> filter = null) where TEntity : class;

        TEntity FirstOrDefault<TEntity>(Expression<Func<TEntity, bool>> filter = null) where TEntity : class;

        TEntity Find<TEntity>(object id) where TEntity : class;

        TEntity Add<TEntity>(TEntity entity) where TEntity : class;

        bool Remove<TEntity>(TEntity entity) where TEntity : class;

        bool Remove<TEntity>(object id) where TEntity : class;

        void SaveChanges();
    }
}
