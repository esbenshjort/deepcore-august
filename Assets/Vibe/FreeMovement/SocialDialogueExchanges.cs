using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public struct SocialDialogueReply
    {
        public SocialResponse Response;
        public string Text;
        public bool FailedJoke; // Accept that lands cold / irritation
    }

    public sealed class SocialDialogueExchange
    {
        public int Id;
        public SocialDialogueTopic Topic;
        public SocialAction Action;
        public string Initiator;
        public SocialDialogueReply[] Replies;
        public string[] Closers;
        public RelationshipClass MinWarmthRel; // Neutral = any; Friendly+ for warm-only
        public bool RequiresHostileRel;
        public bool RequiresBondOrFriend;
        public SocialWeakPointKind NeedsWeak;
        public SocialStrengthKind NeedsStrength;
        public SocialMemoryType NeedsMemory;
        public bool NeedsMemorySet;
        public float Weight = 1f;
    }

    /// <summary>
    /// Curated initiator→compatible response exchanges (presentation only).
    /// Quality over raw count — paired beats prevent unrelated replies.
    /// </summary>
    public static partial class SocialDialogueExchanges
    {
        static readonly List<SocialDialogueExchange> All = new(220);
        static readonly System.Random Rng = new(9041);
        static int _nextId = 1;
        static bool _built;

        /// <summary>Last picked exchange for this presentation (pairs response).</summary>
        public static SocialDialogueExchange ActiveExchange { get; private set; }

        public static void ClearActive() => ActiveExchange = null;

        public static int CatalogCount
        {
            get { EnsureBuilt(); return All.Count; }
        }

        /// <summary>Initiators + all reply/closer strings in catalog.</summary>
        public static int UtteranceVariantCount
        {
            get
            {
                EnsureBuilt();
                int n = 0;
                for (int i = 0; i < All.Count; i++)
                {
                    n++; // initiator
                    if (All[i].Replies != null) n += All[i].Replies.Length;
                    if (All[i].Closers != null) n += All[i].Closers.Length;
                }
                return n;
            }
        }

        static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            Build();
            BuildBibleV21();
        }

        static SocialDialogueReply R(SocialResponse resp, string text, bool failedJoke = false) =>
            new() { Response = resp, Text = text, FailedJoke = failedJoke };

        static void Add(SocialDialogueTopic topic, SocialAction action, string init,
            SocialDialogueReply[] replies, string[] closers = null,
            bool hostile = false, bool bonded = false,
            SocialWeakPointKind weak = SocialWeakPointKind.None,
            SocialStrengthKind strength = SocialStrengthKind.None,
            SocialMemoryType mem = SocialMemoryType.HelpedMe, bool needsMem = false,
            float weight = 1f)
        {
            All.Add(new SocialDialogueExchange
            {
                Id = _nextId++,
                Topic = topic,
                Action = action,
                Initiator = init,
                Replies = replies,
                Closers = closers,
                RequiresHostileRel = hostile,
                RequiresBondOrFriend = bonded,
                NeedsWeak = weak,
                NeedsStrength = strength,
                NeedsMemory = mem,
                NeedsMemorySet = needsMem,
                Weight = weight,
            });
        }

        static void Build()
        {
            // ——— JOKE / HUMOUR ———
            Add(SocialDialogueTopic.Humour, SocialAction.Joke,
                "If the rock wanted us here, it'd leave a door.",
                new[]
                {
                    R(SocialResponse.Accept, "Then why's it charging rent in dust?"),
                    R(SocialResponse.Agree, "Worst landlord I ever had."),
                    R(SocialResponse.Deflect, "Save it for the fire."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Not funny. Dig.", true),
                },
                new[] { "Luxury.", "Ha. Ow.", "Alright, back to it." });

            Add(SocialDialogueTopic.Humour, SocialAction.Joke,
                "My helmet light's brighter than my future.",
                new[]
                {
                    R(SocialResponse.Accept, "At least the helmet's paid for."),
                    R(SocialResponse.Agree, "Speak for yourself. Mine's rented."),
                    R(SocialResponse.Deflect, "Dark humour, dark tunnel. Fitting."),
                    R(SocialResponse.Escalate, "Keep that talk away from me."),
                });

            Add(SocialDialogueTopic.Food, SocialAction.Joke,
                "Stew tonight tasted like boot. Progress.",
                new[]
                {
                    R(SocialResponse.Accept, "Yesterday it tasted like sock. Upgraded."),
                    R(SocialResponse.Agree, "I found a potato. Named it Hope."),
                    R(SocialResponse.Deflect, "Don't insult Kit's cooking where Kit can hear."),
                    R(SocialResponse.PushBack, "Eat and shut up.", true),
                },
                new[] { "Hope died in the pot.", "Seconds?" });

            Add(SocialDialogueTopic.NarrowTunnel, SocialAction.Joke,
                "This shaft's so tight I owe it rent for my shoulders.",
                new[]
                {
                    R(SocialResponse.Accept, "Breathe sideways."),
                    R(SocialResponse.Agree, "I left my dignity at the last bend."),
                    R(SocialResponse.Deflect, "Width-1. Character building."),
                    R(SocialResponse.PushBack, "Then don't come.", true),
                    R(SocialResponse.Escalate, "Scared already?"),
                },
                new[] { "Luxury.", "Want me to hold your hand?", "Just dig." });

            Add(SocialDialogueTopic.Darkness, SocialAction.Joke,
                "I waved at the dark. It didn't wave back. Rude.",
                new[]
                {
                    R(SocialResponse.Accept, "It waved. You just couldn't see it."),
                    R(SocialResponse.Agree, "Pitch black has no manners."),
                    R(SocialResponse.Deflect, "Lamp first. Comedy later."),
                    R(SocialResponse.Ignore, "Mhm."),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Joke,
                "If this bit breaks, I'm filing a complaint with the mountain.",
                new[]
                {
                    R(SocialResponse.Accept, "Good luck getting a receipt."),
                    R(SocialResponse.Agree, "CC the bedrock. They're listening."),
                    R(SocialResponse.Deflect, "Less filing. More drilling."),
                });

            Add(SocialDialogueTopic.Exhaustion, SocialAction.Joke,
                "I've been awake so long my dreams are filing overtime.",
                new[]
                {
                    R(SocialResponse.Accept, "Tell them we don't pay dreams."),
                    R(SocialResponse.Agree, "Same. Mine are unionizing."),
                    R(SocialResponse.Withdraw, "…I need five minutes."),
                    R(SocialResponse.PushBack, "Then sleep on your break.", true),
                });

            // ——— ENCOURAGE / PRAISE ———
            Add(SocialDialogueTopic.Praise, SocialAction.Encourage,
                "You're holding that face clean. Keep it.",
                new[]
                {
                    R(SocialResponse.Accept, "Trying."),
                    R(SocialResponse.Agree, "Means something coming from you."),
                    R(SocialResponse.Deflect, "Just don't jinx it."),
                    R(SocialResponse.Ignore, "…"),
                }, bonded: true);

            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "Say what you want — they don't freeze.",
                new[]
                {
                    R(SocialResponse.Accept, "Not today, anyway."),
                    R(SocialResponse.Agree, "Steady's underrated."),
                    R(SocialResponse.Deflect, "Flattery's slippery down here."),
                },
                strength: SocialStrengthKind.SteadyUnderPressure, weight: 1.3f);

            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "Get them on this. They'll know.",
                new[]
                {
                    R(SocialResponse.Accept, "Already thinking it."),
                    R(SocialResponse.Agree, "Yeah. Trust their eyes."),
                    R(SocialResponse.Deflect, "If they want the headache."),
                },
                strength: SocialStrengthKind.Insight, weight: 1.25f);

            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "Could carry the damn drill home and still complain about dinner.",
                new[]
                {
                    R(SocialResponse.Accept, "Dinner's the hard part."),
                    R(SocialResponse.Agree, "Don't give them ideas."),
                    R(SocialResponse.Deflect, "Save the poetry."),
                },
                strength: SocialStrengthKind.PhysicalPower, weight: 1.2f);

            Add(SocialDialogueTopic.Injury, SocialAction.Encourage,
                "That limp's honest. Sit before it gets clever.",
                new[]
                {
                    R(SocialResponse.Accept, "After this cut."),
                    R(SocialResponse.Agree, "Yeah. Heard."),
                    R(SocialResponse.Deflect, "I've had worse."),
                    R(SocialResponse.PushBack, "Don't mother me."),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Encourage,
                "Slow is fine. Stupid isn't. You're not being stupid.",
                new[]
                {
                    R(SocialResponse.Accept, "I'll take it."),
                    R(SocialResponse.Agree, "High praise."),
                    R(SocialResponse.Deflect, "Barely."),
                });

            // ——— CONNECT ———
            Add(SocialDialogueTopic.Work, SocialAction.Connect,
                "You're putting another support there?",
                new[]
                {
                    R(SocialResponse.Accept, "I enjoy ceilings staying above my head."),
                    R(SocialResponse.Agree, "Until the rock learns manners."),
                    R(SocialResponse.Deflect, "Unless you've got a better idea."),
                    R(SocialResponse.PushBack, "Mind your own span."),
                },
                new[] { "Luxury.", "Fair.", "Carry on." });

            Add(SocialDialogueTopic.Camp, SocialAction.Connect,
                "Fire's still going. That's something.",
                new[]
                {
                    R(SocialResponse.Accept, "Small mercies."),
                    R(SocialResponse.Agree, "Warm's not nothing."),
                    R(SocialResponse.Deflect, "Until the fuel runs."),
                });

            Add(SocialDialogueTopic.Confinement, SocialAction.Connect,
                "You alright in this squeeze?",
                new[]
                {
                    R(SocialResponse.Accept, "Define alright."),
                    R(SocialResponse.Agree, "Breathing. Counting. Digging."),
                    R(SocialResponse.Deflect, "Ask me at camp."),
                    R(SocialResponse.Withdraw, "I need out soon."),
                    R(SocialResponse.PushBack, "I'm fine. Stop staring."),
                });

            Add(SocialDialogueTopic.Callback, SocialAction.Connect,
                "Want me to hold your hand this time?",
                new[]
                {
                    R(SocialResponse.Accept, "Want me to break yours?"),
                    R(SocialResponse.Agree, "Funny. Almost."),
                    R(SocialResponse.Escalate, "Say that again."),
                    R(SocialResponse.Deflect, "Not today."),
                },
                needsMem: true, mem: SocialMemoryType.WasTrapped, bonded: true, weight: 0.7f);

            Add(SocialDialogueTopic.Callback, SocialAction.Joke,
                "Careful. Tunnel's getting narrow.",
                new[]
                {
                    R(SocialResponse.Accept, "I noticed. Thanks."),
                    R(SocialResponse.PushBack, "Don't."),
                    R(SocialResponse.Escalate, "You're enjoying this."),
                    R(SocialResponse.Deflect, "Lamp. Then talk."),
                },
                needsMem: true, mem: SocialMemoryType.WasTrapped, hostile: true, weight: 0.85f);

            // ——— COMPLAIN ———
            Add(SocialDialogueTopic.Exhaustion, SocialAction.Complain,
                "Shift's chewing me raw.",
                new[]
                {
                    R(SocialResponse.Agree, "Same teeth."),
                    R(SocialResponse.Accept, "We're all gum."),
                    R(SocialResponse.Deflect, "Whistle's coming."),
                    R(SocialResponse.PushBack, "Then dig quieter."),
                });

            Add(SocialDialogueTopic.Darkness, SocialAction.Complain,
                "Need another lamp on this stretch. Blind as a boot.",
                new[]
                {
                    R(SocialResponse.Agree, "Engineer hears that? Good."),
                    R(SocialResponse.Accept, "Pitch black's not a vibe."),
                    R(SocialResponse.Deflect, "Helmet's all we've got."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Manager, SocialAction.Complain,
                "Manager wants deeper. Manager isn't breathing this dust.",
                new[]
                {
                    R(SocialResponse.Agree, "Paper doesn't choke."),
                    R(SocialResponse.Accept, "Keep that talk soft."),
                    R(SocialResponse.Deflect, "Orders are orders."),
                    R(SocialResponse.PushBack, "Don't start."),
                });

            Add(SocialDialogueTopic.Geology, SocialAction.Complain,
                "This face is lying. Feels wrong under the bit.",
                new[]
                {
                    R(SocialResponse.Agree, "I felt it too."),
                    R(SocialResponse.Accept, "Mark it. Go slow."),
                    R(SocialResponse.Deflect, "Everything feels wrong after ten hours."),
                });

            // ——— PROVOKE / CONFRONT / WEAK POINTS ———
            Add(SocialDialogueTopic.WeakPoint, SocialAction.Provoke,
                "Slow again. Surprise.",
                new[]
                {
                    R(SocialResponse.PushBack, "Say that when you're on the bit."),
                    R(SocialResponse.Escalate, "Fuck off."),
                    R(SocialResponse.Deflect, "Keep walking."),
                    R(SocialResponse.Withdraw, "…"),
                },
                hostile: true, weak: SocialWeakPointKind.Speed, weight: 1.15f);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Provoke,
                "Careful. Wouldn't want you to freeze.",
                new[]
                {
                    R(SocialResponse.Escalate, "You looking for a fight?"),
                    R(SocialResponse.PushBack, "One more word."),
                    R(SocialResponse.Deflect, "Not worth it."),
                    R(SocialResponse.Agree, "Hilarious. Really.", true),
                },
                hostile: true, weak: SocialWeakPointKind.Fear, weight: 1.2f);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Confront,
                "You missed that tell. Don't miss the next.",
                new[]
                {
                    R(SocialResponse.PushBack, "I saw it. Different call."),
                    R(SocialResponse.Escalate, "Back off."),
                    R(SocialResponse.Accept, "…Fine. Watched."),
                    R(SocialResponse.Deflect, "Write it in your little book."),
                },
                weak: SocialWeakPointKind.Competence, weight: 1.1f);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Provoke,
                "Proud of the muscles. Shame about the judgment.",
                new[]
                {
                    R(SocialResponse.Escalate, "Say that to my face properly."),
                    R(SocialResponse.PushBack, "Keep talking."),
                    R(SocialResponse.Deflect, "Cute."),
                },
                hostile: true, weak: SocialWeakPointKind.StrengthPride, weight: 0.95f);

            Add(SocialDialogueTopic.Work, SocialAction.Confront,
                "That cut's sloppy. Fix it before someone dies in it.",
                new[]
                {
                    R(SocialResponse.Accept, "On it."),
                    R(SocialResponse.PushBack, "It's holding."),
                    R(SocialResponse.Escalate, "You dig it then."),
                    R(SocialResponse.Agree, "…Yeah. You're right."),
                });

            Add(SocialDialogueTopic.Collapse, SocialAction.Confront,
                "Last time we ignored the creak. Remember?",
                new[]
                {
                    R(SocialResponse.Agree, "I remember."),
                    R(SocialResponse.Accept, "Supports. Now."),
                    R(SocialResponse.Deflect, "Don't."),
                    R(SocialResponse.Withdraw, "…"),
                },
                needsMem: true, mem: SocialMemoryType.SurvivedCollapse, weight: 0.9f);

            // ——— MORE WORK / SUPPORTS ———
            Add(SocialDialogueTopic.Supports, SocialAction.Connect,
                "Timber's singing. Hear it?",
                new[]
                {
                    R(SocialResponse.Accept, "I hear it. Brace."),
                    R(SocialResponse.Agree, "Yeah. Don't like that note."),
                    R(SocialResponse.Deflect, "Wood always complains."),
                });

            Add(SocialDialogueTopic.Discovery, SocialAction.Encourage,
                "You called that vein. Don't let anyone rewrite it.",
                new[]
                {
                    R(SocialResponse.Accept, "I'll take the win."),
                    R(SocialResponse.Agree, "Rare enough."),
                    R(SocialResponse.Deflect, "Luck and dirt."),
                },
                strength: SocialStrengthKind.Insight);

            Add(SocialDialogueTopic.Rescue, SocialAction.Encourage,
                "You got them out. That stays.",
                new[]
                {
                    R(SocialResponse.Accept, "Had to."),
                    R(SocialResponse.Agree, "Couldn't leave them."),
                    R(SocialResponse.Deflect, "Anyone would've."),
                },
                needsMem: true, mem: SocialMemoryType.RescuedByWorker, bonded: true);

            Add(SocialDialogueTopic.Trapped, SocialAction.Connect,
                "Path's dead. Breathe. We find another.",
                new[]
                {
                    R(SocialResponse.Accept, "Trying."),
                    R(SocialResponse.Agree, "Don't leave me."),
                    R(SocialResponse.Withdraw, "I can't—"),
                    R(SocialResponse.PushBack, "I know."),
                });

            // Extra humour / camp / work variety
            Add(SocialDialogueTopic.Camp, SocialAction.Joke,
                "If the toilet's occupied, I'm digging a new one.",
                new[]
                {
                    R(SocialResponse.Accept, "Civil engineering at its finest."),
                    R(SocialResponse.Agree, "Kit would cry."),
                    R(SocialResponse.Deflect, "Wait your turn like a human."),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Joke,
                "Hauler's doing laps. Think they enjoy it?",
                new[]
                {
                    R(SocialResponse.Accept, "They enjoy complaining about it."),
                    R(SocialResponse.Agree, "Cardio with consequences."),
                    R(SocialResponse.Deflect, "Someone has to."),
                });

            Add(SocialDialogueTopic.General, SocialAction.Connect,
                "You eat yet?",
                new[]
                {
                    R(SocialResponse.Accept, "Something that claimed to be stew."),
                    R(SocialResponse.Agree, "Enough to keep standing."),
                    R(SocialResponse.Deflect, "Later."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.General, SocialAction.Encourage,
                "We're still here. That's the job.",
                new[]
                {
                    R(SocialResponse.Accept, "Ugly truth."),
                    R(SocialResponse.Agree, "I'll take ugly."),
                    R(SocialResponse.Deflect, "Poetry later."),
                });

            Add(SocialDialogueTopic.Depth, SocialAction.Complain,
                "Feels heavier down here. Not the rock — the air.",
                new[]
                {
                    R(SocialResponse.Agree, "Depth taxes the skull."),
                    R(SocialResponse.Accept, "Lamp helps. Barely."),
                    R(SocialResponse.Deflect, "Keep moving."),
                });

            Add(SocialDialogueTopic.Humour, SocialAction.Joke,
                "I told the rock a joke. It cracked. Finally an audience.",
                new[]
                {
                    R(SocialResponse.Accept, "Don't encourage it."),
                    R(SocialResponse.Agree, "Worst crowd work ever."),
                    R(SocialResponse.PushBack, "Please stop.", true),
                    R(SocialResponse.Ignore, "…"),
                },
                new[] { "Tough room.", "Ha." });

            Add(SocialDialogueTopic.Praise, SocialAction.Encourage,
                "Quiet work. Good work.",
                new[]
                {
                    R(SocialResponse.Accept, "Thanks."),
                    R(SocialResponse.Agree, "I'll take quiet."),
                    R(SocialResponse.Deflect, "Don't make it weird."),
                }, bonded: true);

            Add(SocialDialogueTopic.Work, SocialAction.Complain,
                "Bit's screaming. Or I am. Hard to tell.",
                new[]
                {
                    R(SocialResponse.Agree, "Both, probably."),
                    R(SocialResponse.Accept, "Cool it before it cooks you."),
                    R(SocialResponse.Deflect, "Music to some ears."),
                });

            Add(SocialDialogueTopic.Prospecting, SocialAction.Connect,
                "Scan's quiet. Too quiet, or just empty?",
                new[]
                {
                    R(SocialResponse.Accept, "Empty until it isn't."),
                    R(SocialResponse.Agree, "I hate that answer."),
                    R(SocialResponse.Deflect, "Ask Lewis."),
                });

            Add(SocialDialogueTopic.General, SocialAction.Provoke,
                "You look pleased with yourself.",
                new[]
                {
                    R(SocialResponse.Deflect, "Do I?"),
                    R(SocialResponse.PushBack, "Problem?"),
                    R(SocialResponse.Accept, "Rare day."),
                    R(SocialResponse.Escalate, "Wipe that look off."),
                }, hostile: true);

            Add(SocialDialogueTopic.General, SocialAction.Joke,
                "Ten hours in and I'm inventing new swear words.",
                new[]
                {
                    R(SocialResponse.Accept, "Patent them."),
                    R(SocialResponse.Agree, "Teach me one."),
                    R(SocialResponse.Deflect, "Save some for bedrock."),
                    R(SocialResponse.Ignore, "Mhm."),
                });
        }

        public static string PickInitiator(
            SocialAction action, bool actionOk, SocialContext ctx,
            in SocialDialogueTone tone, in SocialSituationFlags sit,
            int speakerId, int listenerId, float gameHours)
        {
            EnsureBuilt();
            ActiveExchange = null;
            if (!actionOk && (action == SocialAction.Encourage || action == SocialAction.Joke || action == SocialAction.Connect))
            {
                // failed positive — still allow a thin pool via exchanges tagged humour/work
            }

            var hist = SocialDialogueHistory.Instance;
            var candidates = new List<SocialDialogueExchange>(32);
            for (int i = 0; i < All.Count; i++)
            {
                var ex = All[i];
                if (ex.Action != action) continue;
                if (!sit.AllowsTopic(ex.Topic)) continue;
                if (!hist.IsExchangeFresh(ex.Id)) continue;
                if (!hist.IsLineFresh(ex.Initiator, gameHours)) continue;
                if (!hist.IsTopicFresh(ex.Topic) && ex.Topic != SocialDialogueTopic.General) continue;
                if (!RelOk(ex, tone.RelClass)) continue;
                if (ex.NeedsWeak != SocialWeakPointKind.None)
                {
                    if (!SocialPersonKnowledge.Instance.KnowsWeakPoint(speakerId, listenerId, out var w, out _)
                        || w != ex.NeedsWeak) continue;
                    if (tone.RelClass != RelationshipClass.Grudge
                        && tone.RelClass != RelationshipClass.Rivalry
                        && tone.RelClass != RelationshipClass.Strained
                        && !ex.RequiresHostileRel) continue;
                }
                if (ex.NeedsStrength != SocialStrengthKind.None)
                {
                    if (!SocialPersonKnowledge.Instance.KnowsStrength(speakerId, listenerId, out var s, out _)
                        || s != ex.NeedsStrength) continue;
                }
                if (ex.NeedsMemorySet)
                {
                    if (!tone.HasMemoryHint || tone.MemoryHint != ex.NeedsMemory) continue;
                }
                candidates.Add(ex);
            }

            if (candidates.Count == 0) return null;

            // Weight: prefer situational topics, humour when Joke, warmth when bonded
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                float w = candidates[i].Weight;
                if (candidates[i].Topic != SocialDialogueTopic.General
                    && candidates[i].Topic != SocialDialogueTopic.Humour)
                    w *= 1.35f;
                if (action == SocialAction.Joke && candidates[i].Topic == SocialDialogueTopic.Humour)
                    w *= 1.25f;
                total += w;
                // stash in Weight temporarily via list parallel — use local
            }

            float roll = (float)Rng.NextDouble() * total;
            float acc = 0f;
            SocialDialogueExchange pick = candidates[0];
            for (int i = 0; i < candidates.Count; i++)
            {
                float w = candidates[i].Weight;
                if (candidates[i].Topic != SocialDialogueTopic.General
                    && candidates[i].Topic != SocialDialogueTopic.Humour)
                    w *= 1.35f;
                if (action == SocialAction.Joke && candidates[i].Topic == SocialDialogueTopic.Humour)
                    w *= 1.25f;
                acc += w;
                if (roll <= acc) { pick = candidates[i]; break; }
            }

            ActiveExchange = pick;
            hist.Remember(pick.Initiator, pick.Id, pick.Topic, gameHours);
            if (pick.Topic == SocialDialogueTopic.Callback)
                SocialPersonKnowledge.Instance.ObserveRunningJokeFuel(speakerId, gameHours, "DialogueCallback");
            return pick.Initiator;
        }

        public static string PickResponse(
            SocialResponse response, SocialAction action, bool actionOk,
            in SocialDialogueTone tone, float gameHours)
        {
            EnsureBuilt();
            var hist = SocialDialogueHistory.Instance;
            if (ActiveExchange != null && ActiveExchange.Replies != null)
            {
                var pool = new List<SocialDialogueReply>(8);
                for (int i = 0; i < ActiveExchange.Replies.Length; i++)
                {
                    var r = ActiveExchange.Replies[i];
                    if (r.Response != response) continue;
                    if (!hist.IsLineFresh(r.Text, gameHours)) continue;
                    pool.Add(r);
                }
                // Soft fallback: any reply on this exchange if exact response missing
                if (pool.Count == 0)
                {
                    for (int i = 0; i < ActiveExchange.Replies.Length; i++)
                    {
                        var r = ActiveExchange.Replies[i];
                        if (!CompatibleFallback(response, r.Response)) continue;
                        if (!hist.IsLineFresh(r.Text, gameHours)) continue;
                        pool.Add(r);
                    }
                }
                if (pool.Count > 0)
                {
                    var chosen = pool[Rng.Next(pool.Count)];
                    hist.Remember(chosen.Text, ActiveExchange.Id, ActiveExchange.Topic, gameHours);
                    return chosen.Text;
                }
            }
            return null;
        }

        public static string PickCloser(SocialEncounterLog log, float gameHours)
        {
            EnsureBuilt();
            if (ActiveExchange?.Closers == null || ActiveExchange.Closers.Length == 0)
                return null;
            if (Rng.NextDouble() > 0.42) return null;
            var hist = SocialDialogueHistory.Instance;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                string c = ActiveExchange.Closers[Rng.Next(ActiveExchange.Closers.Length)];
                if (hist.IsLineFresh(c, gameHours))
                {
                    hist.Remember(c, ActiveExchange.Id, ActiveExchange.Topic, gameHours);
                    return c;
                }
            }
            return null;
        }

        static bool RelOk(SocialDialogueExchange ex, RelationshipClass rel)
        {
            if (ex.RequiresHostileRel)
                return rel == RelationshipClass.Grudge || rel == RelationshipClass.Rivalry
                       || rel == RelationshipClass.Strained;
            if (ex.RequiresBondOrFriend)
                return rel == RelationshipClass.Friendly || rel == RelationshipClass.Bonded
                       || rel == RelationshipClass.Professional;
            return true;
        }

        static bool CompatibleFallback(SocialResponse want, SocialResponse have)
        {
            if (want == have) return true;
            // Keep reply related: Accept↔Agree, PushBack↔Escalate, Deflect↔Ignore/Withdraw
            return (want, have) switch
            {
                (SocialResponse.Accept, SocialResponse.Agree) => true,
                (SocialResponse.Agree, SocialResponse.Accept) => true,
                (SocialResponse.PushBack, SocialResponse.Escalate) => true,
                (SocialResponse.Escalate, SocialResponse.PushBack) => true,
                (SocialResponse.Deflect, SocialResponse.Ignore) => true,
                (SocialResponse.Ignore, SocialResponse.Deflect) => true,
                (SocialResponse.Deflect, SocialResponse.Withdraw) => true,
                _ => false,
            };
        }
    }
}
