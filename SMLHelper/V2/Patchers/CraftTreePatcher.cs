﻿using System;
using Harmony;
using System.Collections.Generic;
using System.Reflection;

namespace SMLHelper.V2.Patchers
{
    public class CraftTreePatcher
    {
        internal static List<Crafting.CustomCraftTab> CustomTabs = new List<Crafting.CustomCraftTab>();
        internal static List<Crafting.CustomCraftNode> CustomNodes = new List<Crafting.CustomCraftNode>();
        internal static List<Crafting.CraftNodeToScrub> NodesToRemove = new List<Crafting.CraftNodeToScrub>();
        internal static Dictionary<CraftTree.Type, Crafting.CustomCraftTreeRoot> CustomTrees = new Dictionary<CraftTree.Type, Crafting.CustomCraftTreeRoot>();

        #region Node Handling

        private static void AddCustomTabs(ref CraftNode nodes, List<Crafting.CustomCraftTab> customTabs, CraftTree.Type scheme)
        {
            foreach (var tab in customTabs)
            {
                if (tab.Scheme != scheme) continue;

                var path = tab.Path.SplitByChar('/');
                var currentNode = default(TreeNode);
                currentNode = nodes;

                for (int i = 0; i < path.Length; i++)
                {
                    var currentPath = path[i];

                    var node = currentNode[currentPath];
                    if (node == null)
                    {
                        var newNode = new CraftNode(currentPath, TreeAction.Expand, TechType.None);
                        currentNode.AddNode(new TreeNode[]
                        {
                            newNode
                        });

                        node = newNode;
                    }

                    currentNode = node;
                }
            }
        }

        private static void PatchNodes(ref CraftNode nodes, List<Crafting.CustomCraftNode> customNodes, CraftTree.Type scheme)
        {
            foreach (var customNode in customNodes)
            {
                if (!customNode.ItemExists) continue;
                if (customNode.Scheme != scheme) continue;

                var path = customNode.Path.SplitByChar('/');
                var currentNode = default(TreeNode);
                currentNode = nodes;

                for (int i = 0; i < path.Length; i++)
                {
                    var currentPath = path[i];
                    if (i == (path.Length - 1))
                    {
                        break;
                    }

                    currentNode = currentNode[currentPath];
                }

                currentNode.AddNode(new TreeNode[]
                {
                    new CraftNode(path[path.Length - 1], TreeAction.Craft, customNode.TechType)
                });
            }
        }

        private static void RemoveNodes(ref CraftNode nodes, List<Crafting.CraftNodeToScrub> nodesToRemove, CraftTree.Type scheme)
        {
            // This method can be used to both remove single child nodes, thus removing one recipe from the tree.
            // Or it can remove entire tabs at once, removing the tab and all the recipes it contained in one go.

            foreach (var nodeToRemove in nodesToRemove)
            {
                // Not for this fabricator. Skip.
                if (nodeToRemove.Scheme != scheme) continue;

                // Get the names of each node in the path to traverse tree until we reach the node we want.
                var path = nodeToRemove.Path.SplitByChar('/');
                var currentNode = default(TreeNode);
                currentNode = nodes;

                // Travel the path down the tree.
                string currentPath = null;
                for (int step = 0; step < path.Length; step++)
                {
                    currentPath = path[step];
                    if (step > path.Length)
                    {
                        break;
                    }

                    currentNode = currentNode[currentPath];
                }

                // Hold a reference to the parent node
                var parentNode = currentNode.parent;

                // Safty checks.
                if (currentNode != null && currentNode.id == currentPath)
                {
                    currentNode.Clear(); // Remove all child nodes (if any)
                    parentNode.RemoveNode(currentNode); // Remove the node
                }
            }
        }

        #endregion

        #region Patches

        public static void Patch(HarmonyInstance harmony)
        {
            var type = typeof(CraftTree);
            var fabricatorScheme = type.GetMethod("FabricatorScheme", BindingFlags.Static | BindingFlags.NonPublic);
            var constructorScheme = type.GetMethod("ConstructorScheme", BindingFlags.Static | BindingFlags.NonPublic);
            var workbenchScheme = type.GetMethod("WorkbenchScheme", BindingFlags.Static | BindingFlags.NonPublic);
            var seamothUpgradesScheme = type.GetMethod("SeamothUpgradesScheme", BindingFlags.NonPublic | BindingFlags.Static);
            var mapRoomSheme = type.GetMethod("MapRoomSheme", BindingFlags.Static | BindingFlags.NonPublic);
            var cyclopsFabricatorScheme = type.GetMethod("CyclopsFabricatorScheme", BindingFlags.Static | BindingFlags.NonPublic);

            Type patcherClass = typeof(CraftTreePatcher);

            harmony.Patch(fabricatorScheme, null,
                new HarmonyMethod(patcherClass.GetMethod("FabricatorSchemePostfix", BindingFlags.Static | BindingFlags.NonPublic)));

            harmony.Patch(constructorScheme, null,
                new HarmonyMethod(patcherClass.GetMethod("ConstructorSchemePostfix", BindingFlags.Static | BindingFlags.NonPublic)));

            harmony.Patch(workbenchScheme, null,
                new HarmonyMethod(patcherClass.GetMethod("WorkbenchSchemePostfix", BindingFlags.Static | BindingFlags.NonPublic)));

            harmony.Patch(seamothUpgradesScheme, null,
                new HarmonyMethod(patcherClass.GetMethod("SeamothUpgradesSchemePostfix", BindingFlags.Static | BindingFlags.NonPublic)));

            harmony.Patch(mapRoomSheme, null,
                new HarmonyMethod(patcherClass.GetMethod("MapRoomShemePostfix", BindingFlags.Static | BindingFlags.NonPublic)));

            harmony.Patch(cyclopsFabricatorScheme, null,
                new HarmonyMethod(patcherClass.GetMethod("CyclopsFabricatorSchemePostfix", BindingFlags.Static | BindingFlags.NonPublic)));

            if (CustomCraftTreeNode.HasCustomTrees)
            {
                var craftTreeGetTree = type.GetMethod("GetTree", BindingFlags.Static | BindingFlags.Public);
                var craftTreeInitialize = type.GetMethod("Initialize", BindingFlags.Static | BindingFlags.Public);

                harmony.Patch(craftTreeGetTree,
                    new HarmonyMethod(patcherClass.GetMethod("GetTreePreFix", BindingFlags.Static | BindingFlags.NonPublic)), null);

                harmony.Patch(craftTreeInitialize, null,
                    new HarmonyMethod(patcherClass.GetMethod("InitializePostFix", BindingFlags.Static | BindingFlags.NonPublic)));
            }

            Logger.Log($"CraftTreePatcher is done.");
        }

        private static void FabricatorSchemePostfix(ref CraftNode __result)
        {
            RemoveNodes(ref __result, NodesToRemove, CraftTree.Type.Fabricator);
            AddCustomTabs(ref __result, CustomTabs, CraftTree.Type.Fabricator);
            PatchNodes(ref __result, CustomNodes, CraftTree.Type.Fabricator);
        }

        private static void ConstructorSchemePostfix(ref CraftNode __result)
        {
            RemoveNodes(ref __result, NodesToRemove, CraftTree.Type.Constructor);
            AddCustomTabs(ref __result, CustomTabs, CraftTree.Type.Constructor);
            PatchNodes(ref __result, CustomNodes, CraftTree.Type.Constructor);
        }

        private static void WorkbenchSchemePostfix(ref CraftNode __result)
        {
            RemoveNodes(ref __result, NodesToRemove, CraftTree.Type.Workbench);
            AddCustomTabs(ref __result, CustomTabs, CraftTree.Type.Workbench);
            PatchNodes(ref __result, CustomNodes, CraftTree.Type.Workbench);
        }

        private static void SeamothUpgradesSchemePostfix(ref CraftNode __result)
        {
            RemoveNodes(ref __result, NodesToRemove, CraftTree.Type.SeamothUpgrades);
            AddCustomTabs(ref __result, CustomTabs, CraftTree.Type.SeamothUpgrades);
            PatchNodes(ref __result, CustomNodes, CraftTree.Type.SeamothUpgrades);
        }

        private static void MapRoomShemePostfix(ref CraftNode __result)
        {
            RemoveNodes(ref __result, NodesToRemove, CraftTree.Type.MapRoom);
            AddCustomTabs(ref __result, CustomTabs, CraftTree.Type.MapRoom);
            PatchNodes(ref __result, CustomNodes, CraftTree.Type.MapRoom);
        }

        private static void CyclopsFabricatorSchemePostfix(ref CraftNode __result)
        {
            RemoveNodes(ref __result, NodesToRemove, CraftTree.Type.CyclopsFabricator);
            AddCustomTabs(ref __result, CustomTabs, CraftTree.Type.CyclopsFabricator);
            PatchNodes(ref __result, CustomNodes, CraftTree.Type.CyclopsFabricator);
        }

        private static bool GetTreePreFix(CraftTree.Type treeType, ref CraftTree __result)
        {
            if (CustomTrees.ContainsKey(treeType))
            {
                __result = CustomTrees[treeType].CraftTree;
                return false;
            }

            return true;
        }

        private static void InitializePostFix()
        {
            bool craftTreeInitialized = false;
            Type craftTreeClass = typeof(CraftTree);

            craftTreeClass.GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic).GetValue(craftTreeInitialized);

            if (craftTreeInitialized && !Crafting.CustomCraftTreeNode.Initialized)
            {
                foreach (CraftTree.Type cTreeKey in CustomTrees.Keys)
                {
                    CraftTree customTree = CustomTrees[cTreeKey].CraftTree;

                    MethodInfo addToCraftableTech = craftTreeClass.GetMethod("AddToCraftableTech", BindingFlags.Static | BindingFlags.NonPublic);

                    addToCraftableTech.Invoke(null, new[] { customTree });
                }
            }
        }

        #endregion
    }
}