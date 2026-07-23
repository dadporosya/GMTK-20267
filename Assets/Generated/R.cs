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
    public static class PROJECT
    {
        public static class Audio
        {
            public static class Cards
            {
                public static class Activate
                {
                    public static AudioClip activate1 => Resources.Load<AudioClip>("PROJECT/Audio/Cards/Activate/activate1");
                    public static AudioClip activate2 => Resources.Load<AudioClip>("PROJECT/Audio/Cards/Activate/activate2");
                    public static AudioClip activate3 => Resources.Load<AudioClip>("PROJECT/Audio/Cards/Activate/activate3");

                    private static readonly System.Lazy<object[]> _all = new(() =>
                    {
                        var list = new System.Collections.Generic.List<object>
                        {
                            activate1,
                            activate2,
                            activate3,
                        };

                        return list.ToArray();
                    });

                    public static object[] All => _all.Value;
                }
                public static class Slide
                {
                    public static AudioClip cardSlide1 => Resources.Load<AudioClip>("PROJECT/Audio/Cards/Slide/cardSlide1");
                    public static AudioClip cardSlide2 => Resources.Load<AudioClip>("PROJECT/Audio/Cards/Slide/cardSlide2");

                    private static readonly System.Lazy<object[]> _all = new(() =>
                    {
                        var list = new System.Collections.Generic.List<object>
                        {
                            cardSlide1,
                            cardSlide2,
                        };

                        return list.ToArray();
                    });

                    public static object[] All => _all.Value;
                }
                public static class TakeCard
                {
                    public static AudioClip takeCard1 => Resources.Load<AudioClip>("PROJECT/Audio/Cards/TakeCard/takeCard1");
                    public static AudioClip takeCard2 => Resources.Load<AudioClip>("PROJECT/Audio/Cards/TakeCard/takeCard2");
                    public static AudioClip takeCard3 => Resources.Load<AudioClip>("PROJECT/Audio/Cards/TakeCard/takeCard3");
                    public static AudioClip takeCard4 => Resources.Load<AudioClip>("PROJECT/Audio/Cards/TakeCard/takeCard4");

                    private static readonly System.Lazy<object[]> _all = new(() =>
                    {
                        var list = new System.Collections.Generic.List<object>
                        {
                            takeCard1,
                            takeCard2,
                            takeCard3,
                            takeCard4,
                        };

                        return list.ToArray();
                    });

                    public static object[] All => _all.Value;
                }
                public static AudioClip dropCard => Resources.Load<AudioClip>("PROJECT/Audio/Cards/dropCard");
                public static AudioClip flipCard => Resources.Load<AudioClip>("PROJECT/Audio/Cards/flipCard");

                private static readonly System.Lazy<object[]> _all = new(() =>
                {
                    var list = new System.Collections.Generic.List<object>
                    {
                        dropCard,
                        flipCard,
                    };

                    list.AddRange(Activate.All);
                    list.AddRange(Slide.All);
                    list.AddRange(TakeCard.All);

                    return list.ToArray();
                });

                public static object[] All => _all.Value;
            }
            public static class Clock
            {
                public static class Tick
                {
                    public static AudioClip clockTick1 => Resources.Load<AudioClip>("PROJECT/Audio/Clock/Tick/clockTick1");
                    public static AudioClip clockTick2 => Resources.Load<AudioClip>("PROJECT/Audio/Clock/Tick/clockTick2");
                    public static AudioClip clockTick3 => Resources.Load<AudioClip>("PROJECT/Audio/Clock/Tick/clockTick3");
                    public static AudioClip clockTick4 => Resources.Load<AudioClip>("PROJECT/Audio/Clock/Tick/clockTick4");

                    private static readonly System.Lazy<object[]> _all = new(() =>
                    {
                        var list = new System.Collections.Generic.List<object>
                        {
                            clockTick1,
                            clockTick2,
                            clockTick3,
                            clockTick4,
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

                    list.AddRange(Tick.All);

                    return list.ToArray();
                });

                public static object[] All => _all.Value;
            }
            public static class Fire
            {
                public static AudioClip burn1 => Resources.Load<AudioClip>("PROJECT/Audio/Fire/burn1");
                public static AudioClip burn2 => Resources.Load<AudioClip>("PROJECT/Audio/Fire/burn2");

                private static readonly System.Lazy<object[]> _all = new(() =>
                {
                    var list = new System.Collections.Generic.List<object>
                    {
                        burn1,
                        burn2,
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

                list.AddRange(Cards.All);
                list.AddRange(Clock.All);
                list.AddRange(Fire.All);

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
        list.AddRange(PROJECT.All);

        return list.ToArray();
    });

    public static object[] All => _all.Value;
}
