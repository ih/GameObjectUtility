using System;
using System.Collections;
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

            if (childTransform == parentTransform)
            {
                return true;
            }

            while (childTransform.parent != null)
            {
                if (childTransform.parent == parentTransform)
                {
                    return true;
                }

                childTransform = childTransform.parent;
            }

            return false;
        }

        // Scale gameobject to fit in a box
        // Assumes the orientation of the target object is the same as when it will be placed into the container
        // since collider size may change due to rotation
        // (see https://answers.unity.com/questions/1376150/size-of-collider-bounding-box-regardless-of-rotati.html)
        // TODO find a robust way to take this into account when calculated targetScaledSize
        public static void ScaleToFitParent(GameObject targetObject, Vector3 containerSize, float margin = .95f)
        {
            Transform parent = targetObject.transform.parent;
            Vector3 originalLocalPosition = targetObject.transform.localPosition;
            Quaternion originalLocalRotation = targetObject.transform.localRotation;
            targetObject.transform.SetParent(null, false);
            Vector3 targetScaledSize = GetHierarchySize(targetObject);
            
            // find the dimension which has the biggest difference between target and container then scale the target
            // down based on that dimension
            Vector3 sizeDifference = targetScaledSize - containerSize;
            MathUtility.ElementData maxDifferenceData = MathUtility.GetMaxComponent(sizeDifference);


            var targetLocalScale = targetObject.transform.localScale;
            float targetScaleMaxDifference = targetLocalScale[maxDifferenceData.Index];
            float targetTrueSizeMaxDifference = targetScaledSize[maxDifferenceData.Index] / targetScaleMaxDifference;

            float newScaleMaxDifference = containerSize[maxDifferenceData.Index] / targetTrueSizeMaxDifference;

            // lock the dimensions when scaling
            float scaleDifferenceRatio = newScaleMaxDifference / targetLocalScale[maxDifferenceData.Index];
            Vector3 newScale = new Vector3();
            for (int i = 0; i < 3; i++)
            {
                newScale[i] = scaleDifferenceRatio * targetLocalScale[i];
            }

            // Vector3 newScale = new Vector3(containerSize.x / targetTrueSize.x, containerSize.y / targetTrueSize.y,
            //   containerSize.z / targetTrueSize.z);
            targetLocalScale = newScale;
            targetObject.transform.localScale = targetLocalScale * margin;
            targetObject.transform.SetParent(parent, true);
            targetObject.transform.localPosition = originalLocalPosition;
            targetObject.transform.localRotation = originalLocalRotation;
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

        public static void Highlight(GameObject gameObject, Color? color = null)
        {
            if (gameObject == null)
            {
                return;
            }

            ;
            if (gameObject.GetComponent<Outline>() == null)
            {
                gameObject.AddComponent<Outline>();
            }
            gameObject.GetComponent<Outline>().enabled = true;
            gameObject.GetComponent<Outline>().OutlineColor = color ?? Color.green;
            // TODO clean up/make safer
        }

        public static void UnHighlight(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            gameObject.GetComponent<Outline>().enabled = false;
        }

        public static bool IsHighlighted(GameObject gameObject)
        {
            return gameObject.GetComponent<Outline>().enabled;
        }

        public static Vector3 GetHierarchySize(GameObject gameObject)
        {
            Bounds bounds = GetBounds(gameObject);
            // TODO do full traversal instead of just children
            foreach (Transform child in gameObject.transform)
            {
                Bounds childBounds = bounds;
                if (child.GetComponent<Collider>() != null)
                {
                    childBounds = GetBounds(child.gameObject);
                }

                bounds.Encapsulate(childBounds);
            }

            return bounds.size;
        }

        // Use collider then fallback to renderer
        // handles when gameObject collider/renderer are disabled
        public static Bounds GetBounds(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            Bounds bounds;
            if (collider != null)
            {
                if (!collider.enabled)
                {
                    collider.enabled = true;
                    bounds = collider.bounds;
                    collider.enabled = false;
                }
                else
                {
                    bounds = collider.bounds;
                }
            }
            else
            {
                Renderer renderer = gameObject.GetComponent<Renderer>();
                if (renderer == null)
                {
                    throw new ArgumentException(
                        "Cannot calculate size of object that does not have a collider or renderer");
                }

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
            }

            return bounds;
        }

        public static void SetVisibility(bool isVisible, GameObject targetObject)
        {
            foreach (Renderer renderer in targetObject.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = isVisible;
            }

            foreach (Collider collider in targetObject.GetComponentsInChildren<Collider>())
            {
                collider.enabled = isVisible;
            }
        }

        public static bool IsVisible(GameObject targetObject)
        {
            Renderer renderer = targetObject.GetComponentInChildren<Renderer>();
            return renderer != null && renderer.enabled;
        }
    }
}