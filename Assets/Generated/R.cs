using UnityEngine;
using UnityEngine.UI;

// This file is auto-generated. Do not modify manually.

public static class R
{
    public static class ARCHITECTURE
    {
        public static class Audio
        {
            public static class Mixers
            {

                private static readonly System.Lazy<object[]> _all = new(() =>
                {
                    var list = new System.Collections.Generic.List<object>
                    {
                    };

                    return list.ToArray();
                });

                public static object[] All => _all.Value;
            }

            private static readonly System.Lazy<object[]> _all = new(() =>
            {
                var list = new System.Collections.Generic.List<object>
                {
                };

                list.AddRange(Mixers.All);

                return list.ToArray();
            });

            public static object[] All => _all.Value;
        }
        public static class Materials
        {
            public static Material DefaultOutlineMaterial => Resources.Load<Material>("ARCHITECTURE/Materials/DefaultOutlineMaterial");
            public static Material MasterMat => Resources.Load<Material>("ARCHITECTURE/Materials/MasterMat");
            public static Material MasterMatUI => Resources.Load<Material>("ARCHITECTURE/Materials/MasterMatUI");

            private static readonly System.Lazy<object[]> _all = new(() =>
            {
                var list = new System.Collections.Generic.List<object>
                {
                    DefaultOutlineMaterial,
                    MasterMat,
                    MasterMatUI,
                };

                return list.ToArray();
            });

            public static object[] All => _all.Value;
        }
        public static class Prefabs
        {
            public static class UI
            {

                private static readonly System.Lazy<object[]> _all = new(() =>
                {
                    var list = new System.Collections.Generic.List<object>
                    {
                    };

                    return list.ToArray();
                });

                public static object[] All => _all.Value;
            }

            private static readonly System.Lazy<object[]> _all = new(() =>
            {
                var list = new System.Collections.Generic.List<object>
                {
                };

                list.AddRange(UI.All);

                return list.ToArray();
            });

            public static object[] All => _all.Value;
        }

        private static readonly System.Lazy<object[]> _all = new(() =>
        {
            var list = new System.Collections.Generic.List<object>
            {
            };

            list.AddRange(Audio.All);
            list.AddRange(Materials.All);
            list.AddRange(Prefabs.All);

            return list.ToArray();
        });

        public static object[] All => _all.Value;
    }

    private static readonly System.Lazy<object[]> _all = new(() =>
    {
        var list = new System.Collections.Generic.List<object>
        {
        };

        list.AddRange(ARCHITECTURE.All);

        return list.ToArray();
    });

    public static object[] All => _all.Value;
}
