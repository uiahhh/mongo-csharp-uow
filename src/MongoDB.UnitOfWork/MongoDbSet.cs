using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace MongoDB.UnitOfWork
{
    public class MongoDbSet<TEntity> : IMongoDbSet<TEntity>, IDocumentQueryable<TEntity>
        where TEntity : class
    {
        private readonly IMongoContext context;

        public MongoDbSet(IMongoContext context)
        {
            this.context = context;
        }

        public TEntity Find(object id)
        {
            return this.context.Find<TEntity>(id);
        }

        public List<TEntity> ToList()
        {
            //return this.context.ToList<TEntity>(this.predicate);
            return this.context.ToList<TEntity>();
        }

        public TEntity FirstOrDefault(Expression<Func<TEntity, bool>> predicate)
        {
            //return this.context.FirstOrDefault<TEntity>(predicate ?? this.predicate);
            return this.context.FirstOrDefault<TEntity>(predicate);
        }

        public IDocumentQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
        {
            //this.predicate += predicate;
            return this;
        }
    }
}
