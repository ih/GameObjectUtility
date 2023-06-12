using System.Collections.Generic;
using UnityEngine;

namespace AbstractionMachines
{
    public static class GameObjectUtility
    {
        public static bool IsDescendantOf(GameObject child, GameObject parent)
        {
            Transform childTransform = child.transform;
            Transform parentTransform = parent.transform;

            if (childTransform == parentTransform) return true;

            while (childTransform.parent != null)
            {
                if (childTransform.parent == parentTransform) return true;

                childTransform = childTransform.parent;
            }

            return false;
        }

        // Scale gameobject to fit in a box
        // Assumes the orientation of the target object is the same as when it will be placed into the container
        // since collider size may change due to rotation
        // (see https://answers.unity.com/questions/1376150/size-of-collider-bounding-box-regardless-of-rotati.html)
        // TODO find a robust way to take this into account when calculated targetScaledSize
        public static void ScaleToFit(GameObject targetObject, GameObject containerObject, float margin = .95f,
            bool containerRootOnly = false)
        {
            // Isolate and rotate the container object
            Transform containerParent = containerObject.transform.parent;
            int containerSiblingIndex = containerObject.transform.GetSiblingIndex();
            containerObject.transform.SetParent(null, true);
            Quaternion originalContainerRotation = containerObject.transform.rotation;
            containerObject.transform.rotation = Quaternion.identity;

            Transform parent = targetObject.transform.parent;
            Vector3 originalLocalPosition = targetObject.transform.localPosition;
            Quaternion originalLocalRotation = targetObject.transform.localRotation;
            int targetObjectSiblingIndex = targetObject.transform.GetSiblingIndex();
            targetObject.transform.SetParent(null, false);
            // make sure to calculate the size of the container AFTER targetObject has parent set to null in
            // case the parent is the container
            Vector3 containerSize;
            if (containerRootOnly)
                containerSize = GetRotationNormalizedBounds(containerObject).size;
            else
                containerSize = GetRotationNormalizedHierarchyBounds(containerObject).size;

            // rotate the target b/c that affects how its size is calculated due to how bounding boxes work
            targetObject.transform.localRotation = containerObject.transform.rotation;
            Vector3 targetScaledSize = GetRotationNormalizedHierarchyBounds(targetObject).size;
            if (targetScaledSize == Vector3.zero)
            {
                targetObject.transform.SetParent(parent, true);
                targetObject.transform.localPosition = originalLocalPosition;
                targetObject.transform.localRotation = originalLocalRotation;
                targetObject.transform.SetSiblingIndex(targetObjectSiblingIndex);


                containerObject.transform.rotation = originalContainerRotation;
                containerObject.transform.SetParent(containerParent, true);
                containerObject.transform.SetSiblingIndex(containerSiblingIndex);
                return;
            }


            // find the dimension which has the biggest difference between target and container then scale the target
            // down based on that dimension
            Vector3 sizeDifference = targetScaledSize - containerSize;
            MathUtility.ElementData maxDifferenceData = MathUtility.GetMaxComponent(sizeDifference);


            Vector3 targetLocalScale = targetObject.transform.localScale;
            float targetScaleMaxDifference = targetLocalScale[maxDifferenceData.Index];
            float targetTrueSizeMaxDifference = targetScaledSize[maxDifferenceData.Index] / targetScaleMaxDifference;

            float newScaleMaxDifference = containerSize[maxDifferenceData.Index] / targetTrueSizeMaxDifference;

            // lock the dimensions when scaling
            float scaleDifferenceRatio = newScaleMaxDifference / targetLocalScale[maxDifferenceData.Index];
            Vector3 newScale = new Vector3();
            for (int i = 0; i < 3; i++) newScale[i] = scaleDifferenceRatio * targetLocalScale[i];

            // Vector3 newScale = new Vector3(containerSize.x / targetTrueSize.x, containerSize.y / targetTrueSize.y,
            //   containerSize.z / targetTrueSize.z);
            targetLocalScale = newScale;
            targetObject.transform.localScale = targetLocalScale * margin;
            targetObject.transform.SetParent(parent, true);
            targetObject.transform.localPosition = originalLocalPosition;
            targetObject.transform.localRotation = originalLocalRotation;

            containerObject.transform.rotation = originalContainerRotation;
            containerObject.transform.SetParent(containerParent, true);
            containerObject.transform.SetSiblingIndex(containerSiblingIndex);
        }

        // Use this method to prevent the child scale from being skeweed
        // http://answers.unity.com/answers/417236/view.html
        public static void SetParentPreserveScale(GameObject child, GameObject parent)
        {
            GameObject empty = new GameObject();
            // child.transform.SetParent(parent.transform, true);
            empty.transform.position = child.transform.position;
            empty.transform.SetParent(parent.transform);
            child.transform.SetParent(empty.transform);
        }

        public static void SetScaleToSize(GameObject targetObject, Vector3 size)
        {
            Vector3 targetObjectSize = GetRotationNormalizedHierarchyBounds(targetObject).size;
            // TODO review to see if this is correct 
            float newScaleX = size.x / targetObjectSize.x * targetObject.transform.localScale.x;
            float newScaleY = size.y / targetObjectSize.y * targetObject.transform.localScale.y;
            float newScaleZ = size.z / targetObjectSize.z * targetObject.transform.localScale.z;

            targetObject.transform.localScale = new Vector3(newScaleX, newScaleY, newScaleZ);
        }

        public static void Highlight(GameObject gameObject, Color? color = null)
        {
            if (gameObject == null) return;

            ;
            if (gameObject.GetComponent<Outline>() == null) gameObject.AddComponent<Outline>();
            gameObject.GetComponent<Outline>().enabled = true;
            gameObject.GetComponent<Outline>().OutlineColor = color ?? Color.green;
            // TODO clean up/make safer
        }

        public static void UnHighlight(GameObject gameObject)
        {
            if (gameObject == null) return;

            gameObject.GetComponent<Outline>().enabled = false;
        }

        public static bool IsHighlighted(GameObject gameObject)
        {
            return gameObject.GetComponent<Outline>().enabled;
        }

        // Normalized by rotation to 0 when detached from parent
        public static Bounds GetRotationNormalizedHierarchyBounds(GameObject gameObject)
        {
            Bounds bounds = GetRotationNormalizedBounds(gameObject);
            // TODO do full traversal instead of just children
            var childQueue = new Queue<Transform>();
            childQueue.Enqueue(gameObject.transform);
            while (childQueue.Count > 0)
            {
                Transform current = childQueue.Dequeue();
                foreach (Transform child in current) childQueue.Enqueue(child);
                Bounds currentBounds = GetRotationNormalizedBounds(current.gameObject);
                if (currentBounds.extents != Vector3.zero) bounds.Encapsulate(currentBounds);
            }


            return bounds;
        }

        // Use renderer bounds because they have a world based size as opposed to collider, which is local
        // returns empty bounds if there is no renderer
        // Normalized by rotation to 0 when detached from parent
        public static Bounds GetRotationNormalizedBounds(GameObject gameObject)
        {

            Bounds bounds;
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning("Cannot calculate size of object that does not have a collider or renderer");
                Bounds emptyBounds = new Bounds();
                emptyBounds.center = gameObject.transform.position;
                return emptyBounds;
            }

            Transform gameObjectParent = gameObject.transform.parent;
            int gameObjectSiblingIndex = gameObject.transform.GetSiblingIndex();
            gameObject.transform.SetParent(null, true);
            Quaternion originalRotation = gameObject.transform.rotation;
            gameObject.transform.rotation = Quaternion.identity;
            if (!renderer.enabled)
            {
                renderer.enabled = true;
                bounds = renderer.bounds;
                renderer.enabled = false;
            }
            else
            {
                bounds = renderer.bounds;
            }

            gameObject.transform.rotation = originalRotation;
            gameObject.transform.SetParent(gameObjectParent, true);
            gameObject.transform.SetSiblingIndex(gameObjectSiblingIndex);
            return bounds;
        }

        public static void SetVisibility(bool isVisible, GameObject targetObject)
        {
            foreach (Renderer renderer in targetObject.GetComponentsInChildren<Renderer>())
                renderer.enabled = isVisible;

            foreach (Collider collider in targetObject.GetComponentsInChildren<Collider>())
                collider.enabled = isVisible;
        }

        public static bool IsVisible(GameObject targetObject)
        {
            Renderer renderer = targetObject.GetComponentInChildren<Renderer>();
            return renderer != null && renderer.enabled;
        }

        public static Color GetHighlightColor(GameObject gameObject)
        {
            return gameObject.GetComponent<Outline>().OutlineColor;
        }

        public static bool HasSize(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            Renderer renderer = gameObject.GetComponent<Renderer>();
            return collider || renderer;
        }
        
        public static int GetHiearchyDepth(GameObject gameObject)
        {
            int depth = 0;
            Transform transform = gameObject.transform;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }
            return depth; 
        }
    }

}