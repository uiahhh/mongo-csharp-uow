using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MongoDB.UnitOfWork
{
    public class MongoContext : IMongoContext
    {
        private readonly IMongoDatabase database;
        
        private ConcurrentDictionary<string, ConcurrentDictionary<object, object>> collections;
        private ConcurrentDictionary<string, ConcurrentDictionary<object, string>> originalCollections;
        private ConcurrentDictionary<string, ConcurrentDictionary<object, object>> collectionsToRemove;

        public MongoContext(string connectionString, string databaseName)
        {
            //TODO: database deve ser Lazy
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);

            this.database = database;
            this.CleanDictionaries();
        }

        public MongoContext(IMongoDatabase database)
        {
            this.database = database;
            this.CleanDictionaries();
        }

        private void CleanDictionaries()
        {
            this.collections = new ConcurrentDictionary<string, ConcurrentDictionary<object, object>>();
            this.originalCollections = new ConcurrentDictionary<string, ConcurrentDictionary<object, string>>();
            this.collectionsToRemove = new ConcurrentDictionary<string, ConcurrentDictionary<object, object>>();
        }

        public IMongoDbSet<TEntity> Set<TEntity>()
            where TEntity : class
        {
            //TODO: fazer cache com um dicionario Type,Object
            return new MongoDbSet<TEntity>(this);
        }

        public List<TEntity> All<TEntity>()
                    where TEntity : class
        {
            return ToList<TEntity>(null);
        }

        public List<TEntity> ToList<TEntity>(Expression<Func<TEntity, bool>> filter = null)
            where TEntity : class
        {
            // TODO: necessidade de ter algo configuravel, como no fluent do EF
            var collectionName = typeof(TEntity).Name;

            Expression<Func<TEntity, bool>> noFilter = _ => true;

            var entities = this.database
                                .GetCollection<TEntity>(collectionName)
                                .Find(filter ?? noFilter)
                                .ToListAsync()
                                .Result;

            // TODO: implementar o AsNoTracking
            //Tracking(id, entity);

            return entities
                .Select(x => GetEntity<TEntity>(GetId(x), (id) => x))
                .ToList();
        }

        public TEntity FirstOrDefault<TEntity>(Expression<Func<TEntity, bool>> filter = null)
            where TEntity : class
        {
            // TODO: necessidade de ter algo configuravel, como no fluent do EF
            var collectionName = typeof(TEntity).Name;

            Expression<Func<TEntity, bool>> noFilter = _ => true;

            var entity = this.database
                                .GetCollection<TEntity>(collectionName)
                                .Find(filter ?? noFilter)
                                .FirstOrDefaultAsync()
                                .Result;

            // TODO: implementar o AsNoTracking
            //Tracking(id, entity);

            return GetEntity<TEntity>(GetId(entity), (id) => entity);
        }

        public TEntity Find<TEntity>(object id)
            where TEntity : class
        {
            return this.GetEntity<TEntity>(id, FindInDatabase<TEntity>);
        }

        private TEntity FindInDatabase<TEntity>(object id)
            where TEntity : class
        {
            // TODO: necessidade de ter algo configuravel, como no fluent do EF
            var collectionName = typeof(TEntity).Name;

            var filter = Builders<TEntity>.Filter.Eq("_id", id);

            var entity = this.database
                                .GetCollection<TEntity>(collectionName)
                                .Find(filter)
                                .FirstOrDefaultAsync()
                                .Result;

            // TODO: implementar o AsNoTracking
            Tracking(id, entity);

            return entity;
        }

        private TEntity GetEntity<TEntity>(object id, Func<object, object> entityFactory)
            where TEntity : class
        {
            // TODO: necessidade de ter algo configuravel, como no fluent do EF
            var collectionName = typeof(TEntity).Name;

            // Nao precisa verificar isso, assim é o comportamento do EF
            //var entitiesToRemove = this.collectionsToRemove.GetOrAdd(collectionName, (key) => new ConcurrentDictionary<object, object>());
            //var entityMarkedToRemove = entitiesToRemove.TryGetValue(id, out _);

            //if (entityMarkedToRemove)
            //{
            //    return null;
            //}

            var entities = this.collections.GetOrAdd(collectionName, (key) => new ConcurrentDictionary<object, object>());
            var entity = entities.GetOrAdd(id, entityFactory) as TEntity;

            if (entity == null)
            {
                entities.TryRemove(id, out _);
            }

            return entity;
        }

        private void Tracking(object id, object entity)
        {
            // TODO: necessidade de ter algo configuravel, como no fluent do EF
            var collectionName = entity.GetType().Name;

            var originals = this.originalCollections.GetOrAdd(collectionName, (key) => new ConcurrentDictionary<object, string>());
            var entitySerialized = Serialize(entity);
            originals.TryAdd(id, entitySerialized);
        }

        public TEntity Add<TEntity>(TEntity entity)
            where TEntity : class
        {
            // TODO: necessidade de ter algo configuravel, como no fluent do EF
            var collectionName = typeof(TEntity).Name;
            
            var id = GetId(entity);

            var entities = this.collections.GetOrAdd(collectionName, (key) => new ConcurrentDictionary<object, object>());

            if (!entities.TryAdd(id, entity))
            {
                throw new Exception("PrimaryKey violation");
            }

            return entity;
        }

        private object GetId<TEntity>(TEntity entity) 
            where TEntity : class
        {
            // TODO: Id é o default, mas pode ser configurado outra propriedade para PK
            return typeof(TEntity).GetProperty("Id").GetValue(entity, null);
        }

        public bool Remove<TEntity>(TEntity entity)
            where TEntity : class
        {
            var id = GetId(entity);
            return Remove<TEntity>(id);
        }

        public bool Remove<TEntity>(object id)
            where TEntity : class
        {
            // TODO: necessidade de ter algo configuravel, como no fluent do EF
            var collectionName = typeof(TEntity).Name;

            var entities = this.collectionsToRemove.GetOrAdd(collectionName, (key) => new ConcurrentDictionary<object, object>());

            return entities.TryAdd(id, null);
        }

        private string Serialize(object entity)
        {
            return JsonConvert.SerializeObject(entity); // TODO: remover newtonsoft
        }

        public void SaveChanges()
        {
            //TODO: incluir uma transaction

            //TODO: pensar em como fazer um lock, ou lançar exception se chamar algum dos outros metodos

            foreach (var collection in this.collectionsToRemove)
            {
                var collectionName = collection.Key;
                var entities = collection.Value;

                var collectionDB = this.database.GetCollection<object>(collectionName);

                foreach (var entity in entities)
                {
                    var id = entity.Key;

                    //TODO: substituir por DeleteMany
                    var filter = Builders<object>.Filter.Eq("_id", id);
                    collectionDB.DeleteOne(filter);
                }
            }

            foreach (var collection in this.collections)
            {
                //TODO: a ordem das operacoes faz diferenca? se fizer, temos que ordenar

                var collectionName = collection.Key;
                var entities = collection.Value;
                var originals = this.originalCollections.GetOrAdd(collectionName, (key) => new ConcurrentDictionary<object, string>());                                
                var entitiesToRemove = this.collectionsToRemove.GetOrAdd(collectionName, (key) => new ConcurrentDictionary<object, object>());

                var collectionDB = this.database.GetCollection<object>(collectionName);

                foreach (var entity in entities)
                {
                    var id = entity.Key;
                    var data = entity.Value;                    

                    var entityMarkedToRemove = entitiesToRemove.TryGetValue(id, out _);
                    var shouldBeSave = ShouldBeSave(id, data, originals);

                    if (!entityMarkedToRemove && shouldBeSave)
                    {
                        var filter = Builders<object>.Filter.Eq("_id", id);
                        // TODO: pensar em ter o timestamp para concorrencia
                        // TODO: pensar em ter created e updated para audit
                        // TODO: pensar em ter createdby e updatedby para audit, necessario implementar IMongoAuditAuth
                        collectionDB.ReplaceOne(
                                    filter,
                                    data,
                                    new ReplaceOptions { IsUpsert = true });
                    }
                }
            }
        }

        private bool ShouldBeSave(object id, object data, ConcurrentDictionary<object, string> originals)
        {
            var shouldBeSave = true;

            if (originals.TryGetValue(id, out string originalValue))
            {
                var dataSerialized = Serialize(data);
                shouldBeSave = !JToken.DeepEquals(originalValue, dataSerialized);
            }

            return shouldBeSave;
        }
    }
}
