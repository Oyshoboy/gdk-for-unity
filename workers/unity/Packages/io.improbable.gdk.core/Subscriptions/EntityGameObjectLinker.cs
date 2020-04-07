using System;
using System.Collections.Generic;
using System.Linq;
using Improbable.Gdk.Core;
using Unity.Entities;
using UnityEngine;

namespace Improbable.Gdk.Subscriptions
{
    public class EntityGameObjectLinker
    {
        private readonly WorkerSystem workerSystem;
        private readonly SubscriptionSystem subscriptionSystem;
        private readonly RequireLifecycleSystem lifecycleSystem;
        private readonly EntityManager entityManager;
        private readonly World world;

        internal readonly Dictionary<EntityId, List<GameObject>> EntityIdToGameObjects =
            new Dictionary<EntityId, List<GameObject>>();

        private readonly Dictionary<GameObject, List<RequiredSubscriptionsInjector>> gameObjectToInjectors =
            new Dictionary<GameObject, List<RequiredSubscriptionsInjector>>();

        private readonly Dictionary<GameObject, List<ComponentType>> gameObjectToComponentsAdded =
            new Dictionary<GameObject, List<ComponentType>>();

        public EntityGameObjectLinker(World world)
        {
            this.world = world;
            entityManager = world.EntityManager;

            workerSystem = world.GetExistingSystem<WorkerSystem>();
            lifecycleSystem = world.GetExistingSystem<RequireLifecycleSystem>();

            workerSystem = world.GetExistingSystem<WorkerSystem>();
            if (workerSystem == null)
            {
                throw new ArgumentException(
                    $"Can not create {nameof(EntityGameObjectLinker)}. {world.Name} does not contain a {nameof(workerSystem)}");
            }

            subscriptionSystem = world.GetExistingSystem<SubscriptionSystem>();

            if (subscriptionSystem == null)
            {
                throw new ArgumentException(
                    $"Can not create {nameof(EntityGameObjectLinker)}. {world.Name} does not contain a {nameof(SubscriptionSystem)}");
            }
        }

        public void LinkGameObjectToSpatialOSEntity(EntityId entityId, GameObject gameObject,
            params Type[] componentTypesToAdd)
        {
            var linkedComponent = gameObject.GetComponent<LinkedEntityComponent>();
            if (linkedComponent == null)
            {
                linkedComponent = gameObject.AddComponent<LinkedEntityComponent>();
            }
            else if (linkedComponent.IsValid)
            {
                throw new InvalidOperationException(
                    $"GameObject is already linked to the entity with ID {linkedComponent.EntityId}");
            }

            if (!EntityIdToGameObjects.TryGetValue(entityId, out var linkedGameObjects))
            {
                linkedGameObjects = new List<GameObject>();
                EntityIdToGameObjects.Add(entityId, linkedGameObjects);
            }

            linkedGameObjects.Add(gameObject);

            linkedComponent.IsValid = true;
            linkedComponent.EntityId = entityId;
            linkedComponent.World = world;
            linkedComponent.Worker = workerSystem;
            linkedComponent.Linker = this;

            var injectors = new List<RequiredSubscriptionsInjector>();

            foreach (var component in gameObject.GetComponents<MonoBehaviour>())
            {
                if (component == null)
                {
                    // todo consider - could also tell the user that they have a bad ref here
                    continue;
                }

                var type = component.GetType();

                var isSpatialMonobehaviour = RequiredSubscriptionsDatabase.HasWorkerTypeRequirement(type) || RequiredSubscriptionsDatabase.HasRequiredSubscriptions(type);

                // Note that `WorkerTypeMatchesRequirements` returns true if there is no worker type requirement.
                if (isSpatialMonobehaviour && RequiredSubscriptionsDatabase.WorkerTypeMatchesRequirements(workerSystem.WorkerType, type))
                {
                    // todo this should possibly happen when the command buffer is flushed too
                    injectors.Add(new RequiredSubscriptionsInjector(component, entityId, subscriptionSystem,
                        target => lifecycleSystem.EnableMonoBehaviour((MonoBehaviour) target),
                        target => lifecycleSystem.DisableMonoBehaviour((MonoBehaviour) target)));
                }
            }

            gameObjectToInjectors.Add(gameObject, injectors);

            if (entityId.IsValid())
            {
                AddComponentsToEntity(entityId, gameObject, componentTypesToAdd);
            }
        }

        public void UnlinkGameObjectFromEntity(EntityId entityId, GameObject gameObject)
        {
            if (gameObject == null)
            {
                throw new ArgumentException($"Can not unlink null GameObject from entity {entityId}");
            }

            if (!EntityIdToGameObjects.TryGetValue(entityId, out var gameObjectSet) ||
                !gameObjectSet.Contains(gameObject))
            {
                throw new ArgumentException(
                    $"Can not unlink GameObject {gameObject.name} from entity {entityId}. Not previously linked.");
            }

            var injectors = gameObjectToInjectors[gameObject];

            if (entityId.IsValid() && workerSystem.TryGetEntity(entityId, out var entity))
            {
                foreach (var componentType in gameObjectToComponentsAdded[gameObject])
                {
                    entityManager.RemoveComponent(entity, componentType);
                }
            }

            gameObjectToComponentsAdded.Remove(gameObject);

            var linkComponent = gameObject.GetComponent<LinkedEntityComponent>();
            if (linkComponent != null)
            {
                linkComponent.IsValid = false;
                linkComponent.EntityId = new EntityId(id: 0);
                linkComponent.World = null;
                linkComponent.Worker = null;
                linkComponent.Linker = null;
            }

            foreach (var injector in injectors)
            {
                injector.CancelSubscriptions();
            }

            gameObjectToInjectors.Remove(gameObject);

            gameObjectSet.Remove(gameObject);
            if (gameObjectSet.Count == 0)
            {
                EntityIdToGameObjects.Remove(entityId);
            }
        }

        public void UnlinkAllGameObjects()
        {
            var ids = EntityIdToGameObjects.Keys.ToArray();
            foreach (var id in ids)
            {
                UnlinkAllGameObjectsFromEntityId(id);
            }
        }

        public void UnlinkAllGameObjectsFromEntityId(EntityId entityId)
        {
            if (!EntityIdToGameObjects.TryGetValue(entityId, out var gameObjectSet))
            {
                return;
            }

            while (gameObjectSet.Count > 0)
            {
                UnlinkGameObjectFromEntity(entityId, gameObjectSet[gameObjectSet.Count - 1]);
            }
        }

        private void AddComponentsToEntity(EntityId entityId, GameObject gameObject,
            params Type[] componentTypesToAdd)
        {
            if (!workerSystem.TryGetEntity(entityId, out var entity))
            {
                throw new ArgumentException(
                    $"Can not add GameObject components to entity {entityId}. Entity not in view");
            }

            var componentTypes = new List<ComponentType>(componentTypesToAdd.Length);
            gameObjectToComponentsAdded.Add(gameObject, componentTypes);

            foreach (var type in componentTypesToAdd)
            {
                if (!type.IsSubclassOf(typeof(Component)))
                {
                    throw new InvalidOperationException(
                        $"Can not add {type.Name} to an ECS Entity. Linked components must be derived from Component or MonoBehaviour");
                }

                var c = gameObject.GetComponent(type);
                if (c != null)
                {
                    var componentType = new ComponentType(type);
                    componentTypes.Add(componentType);
                    entityManager.AddComponentObject(entity, c);
                }
            }
        }
    }
}
