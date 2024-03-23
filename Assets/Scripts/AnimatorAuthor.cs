using System;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR



[CustomPropertyDrawer(typeof(GameObjectWithComponentIndexArray))]
public class GameObjectWithComponentIndexArrayPropertyDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var gameObjectProperty = property.FindPropertyRelative("GameObject");
        var componentIndicesProperty = property.FindPropertyRelative("ComponentIndices");
        
        var container = new VisualElement();
        var gameObjectField = new ObjectField("GameObject to Spawn")
        {
            objectType = typeof(GameObject),
            value = gameObjectProperty.objectReferenceValue,
            allowSceneObjects = false
        };
        container.Add(gameObjectField);

        var foldout = new Foldout
        {
            text = "Components to add to Entity",
            style =
            {
                backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f),
                borderBottomLeftRadius = 5,
                borderBottomRightRadius = 5,
                borderTopLeftRadius = 5,
                borderTopRightRadius = 5
            }
        };
        container.Add(foldout);
        
        var toggleRowList = new ScrollView(ScrollViewMode.Vertical);
        foldout.Add(toggleRowList);
        toggleRowList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        toggleRowList.style.maxHeight = 200;
        
        FillScrollViewWithComponents(componentIndicesProperty, gameObjectProperty, toggleRowList);
        gameObjectField.RegisterValueChangedCallback(evt =>
        {
            gameObjectProperty.objectReferenceValue = evt.newValue;
            componentIndicesProperty.ulongValue = 0;
            toggleRowList.Clear();
            FillScrollViewWithComponents(componentIndicesProperty, gameObjectProperty, toggleRowList);
            componentIndicesProperty.serializedObject.ApplyModifiedProperties();
        });
        
        return container;
    }

    static void FillScrollViewWithComponents(SerializedProperty componentIndicesProperty,
        SerializedProperty gameObjectProperty, ScrollView toggleRowList)
    {
        if (gameObjectProperty.objectReferenceValue is not GameObject gameObject)
            return;
        
        var componentIndices = componentIndicesProperty.ulongValue;
        var drawDarker = false;
        var currentBit = 1ul;
        
        foreach (var component in gameObject.GetComponents<Component>())
        {
            var toggle = new Toggle($"{component.GetType().Name}")
            {
                value = (componentIndices & currentBit) != 0,
                style =
                {
                    flexDirection = FlexDirection.RowReverse,
                    alignSelf = Align.Stretch,
                    unityTextAlign = TextAnchor.MiddleRight,
                    backgroundColor = drawDarker ? new Color(0.1f, 0.1f, 0.1f, 0.1f) : new Color(0.1f, 0.1f, 0.1f, 0.2f),
                    marginRight = 5,
                }
            };

            // This is a closure, so we need to capture the current bit
            var bitForCurrentToggle = currentBit;
            toggle.RegisterValueChangedCallback(evt =>
            {
                var componentIndicesForToggle = componentIndicesProperty.ulongValue;
                componentIndicesProperty.ulongValue = evt.newValue 
                    ? componentIndicesForToggle | bitForCurrentToggle 
                    : componentIndicesForToggle & ~bitForCurrentToggle;
                componentIndicesProperty.serializedObject.ApplyModifiedProperties();
            });
            
            toggleRowList.Add(toggle);
            currentBit <<= 1;
            drawDarker = !drawDarker;
        }
    }
}

[Serializable]
public struct GameObjectWithComponentIndexArray
{
    public GameObject GameObject;
    public ulong ComponentIndices;
}

public class AnimatorAuthor : MonoBehaviour
{
    [SerializeField] bool UseDefaultEditorVisualizer = true;
    [SerializeField] GameObjectWithComponentIndexArray GameObjectWithComponents;
    
    class AnimatorAuthorBaker : Baker<AnimatorAuthor>
    {
        public override void Bake(AnimatorAuthor authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            
            if (authoring.GameObjectWithComponents.GameObject == null)
                return;
            
            // GameObjectWithComponents to ComponentTypeSet
            var componentTypes = new FixedList128Bytes<ComponentType>();
            var componentIndices = authoring.GameObjectWithComponents.ComponentIndices;
            foreach (var component in authoring.GameObjectWithComponents.GameObject.GetComponents(typeof(Component)))
            {
                if ((componentIndices & 1) != 0) 
                    componentTypes.Add(ComponentType.ReadWrite(component.GetType()));
                componentIndices >>= 1;
            }
            // AddComponent(entity, new ComponentTypeSet(componentTypes)); -- Requires EM.SetComponentObject to be exposed
            
            AddComponent(entity, new AnimatorInstantiationData
            {
                GameObject = authoring.GameObjectWithComponents.GameObject,
                ComponentIndices = authoring.GameObjectWithComponents.ComponentIndices
            });
            
            if (IsBakingForEditor() && authoring.UseDefaultEditorVisualizer)
            {
                AddComponent(entity, new EditorAnimatorVisualEntityPrefab
                {
                    Prefab = GetEntity(authoring.GameObjectWithComponents.GameObject.gameObject, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
#endif
