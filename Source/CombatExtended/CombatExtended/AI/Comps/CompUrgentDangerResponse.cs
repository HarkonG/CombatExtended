﻿using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended.Utilities;
using Mono.Security.X509.Extensions;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CombatExtended.AI
{
    public class CompUrgentDangerResponse : ICompTactics
    {        
        private const int CELLSAHEAD = 6;        
        private const float FOCUSWEIGHT = 0.4f;
        private const float VISIONWEIGHT = 0.4f;
        private const float HEARINGWEIGHT = 0.2f;
        private const float AWARENESSMIN = 0.5f;

        private bool _disabled = false;
        
        private int _cooldownTick = -1;
        private IntVec3 _lastCell;

        public override int Priority => 1200;
        private float Vision
        {
            get
            {
                if (SelPawn.health?.capacities == null)
                {
                    return 0f;
                }
                if (!SelPawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight))
                {
                    return 0f;
                }
                return SelPawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
            }
        }
        private float Focus
        {
            get
            {
                if (SelPawn.health?.capacities == null)
                {
                    return 0f;
                }
                if (!SelPawn.health.capacities.CapableOf(PawnCapacityDefOf.Consciousness))
                {
                    return 0f;
                }
                return SelPawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            }
        }
        private float Hearing
        {
            get
            {
                if (SelPawn.health?.capacities == null)
                {
                    return 0f;
                }
                if (!SelPawn.health.capacities.CapableOf(PawnCapacityDefOf.Hearing))
                {
                    return 0f;
                }
                return SelPawn.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
            }
        }

        public CompUrgentDangerResponse()
        {
        }

        public override void Initialize(Pawn pawn)
        {
            base.Initialize(pawn);
            _disabled = !pawn.RaceProps.Humanlike || pawn.RaceProps.intelligence != Intelligence.Humanlike || pawn.Faction == null;                
        }

        public override void TickShort()
        {
            base.TickShort();
            if (_disabled || _cooldownTick > GenTicks.TicksGame)
            {
                return;
            }
            Pawn pawn = SelPawn;
            if (_lastCell == pawn.Position)
            {
                _lastCell = IntVec3.Invalid;
                _cooldownTick = GenTicks.TicksGame + 30;
                return;
            }
            PawnPath path = pawn.pather?.curPath ?? null;
            if (path == null || !pawn.pather.moving)
            {
                _lastCell = pawn.Position;
                _cooldownTick = GenTicks.TicksGame + 60;
                return;
            }
            if (pawn.Faction == null || pawn.Drafted || (CurrentWeapon?.def.IsMeleeWeapon ?? true))
            {
                _cooldownTick = GenTicks.TicksGame + GenTicks.TickRareInterval;
                return;
            }
            if (path.NodesLeftCount <= CELLSAHEAD * 2)
            {
                return;
            }
            float vision = Vision, hearing = Hearing, focus = Focus;
            float awarness = vision * VISIONWEIGHT + focus * FOCUSWEIGHT + hearing * HEARINGWEIGHT;
            if (awarness < AWARENESSMIN)
            {
                _cooldownTick = GenTicks.TicksGame + GenTicks.TickRareInterval;
                return;
            }
            List<IntVec3> cells = GenAdjFast.AdjacentCells8Way(path.nodes[path.curNodeIndex - CELLSAHEAD]);
            Map map = Map;
            Pawn enemy = null;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].InBounds(map))
                {
                    Pawn other = cells[i].GetFirstPawn(map);
                    if (other?.HostileTo(SelPawn) ?? false)
                    {
                        enemy = other;
                        break;
                    }
                }
            }
            if (enemy != null)
            {
                Verb attackVerb = CurrentWeapon.GetComp<CompEquippable>()?.PrimaryVerb ?? null;
                if (attackVerb == null || !(CurrentWeaponCompAmmo?.CanBeFiredNow ?? true))
                {
                    _cooldownTick = GenTicks.TicksGame + GenTicks.TickLongInterval;
                    return;
                }
                float range = attackVerb.EffectiveRange;

                if (attackVerb.verbProps.warmupTime < 0.5f && Rand.Chance(focus / 2f))
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Wait_Combat, Rand.Range(300, 500), checkOverrideOnExpiry: true);
                    if (job != null)
                    {
                        pawn.jobs.StopAll();
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                    }
                    _cooldownTick = GenTicks.TicksGame + GenTicks.TickRareInterval * 2;
                }
                if (attackVerb.verbProps.warmupTime > 0.3f && Rand.Chance(focus / 2f))
                {
                    Job job = SuppressionUtility.GetRunForCoverJob(pawn, enemy.Position);
                    if (job != null)
                    {
                        pawn.jobs.StopAll();
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                    }
                    _cooldownTick = GenTicks.TicksGame + GenTicks.TickRareInterval * 3;
                }               
            }
        }        
    }
}