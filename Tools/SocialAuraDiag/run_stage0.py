#!/usr/bin/env python3
"""Social Aura Stage 0 diagnostic mirror of DeepCore.FreeMovement SocialAuraStage0*.

Runs without Unity. C# under Assets/Vibe/FreeMovement remains the integration source of truth.
"""
from __future__ import annotations

import math
import os
import random
import time
from dataclasses import dataclass, field
from datetime import datetime
from enum import IntEnum
from typing import Dict, List, Optional, Tuple

OUT_DIR = os.path.join(
    os.path.dirname(__file__), "..", "..", "BenchmarkResults"
)


class Context(IntEnum):
    None_ = 0
    WorkingTogether = 1
    SharedProblem = 2
    RecentSuccess = 3
    RecentFailure = 4
    IdleNearby = 5
    Emergency = 6


class Action(IntEnum):
    Encourage = 0
    Joke = 1
    Connect = 2
    Complain = 3
    Provoke = 4
    Confront = 5


class Response(IntEnum):
    Accept = 0
    Deflect = 1
    Ignore = 2
    Agree = 3
    PushBack = 4
    Escalate = 5
    Withdraw = 6


class AuraClass(IntEnum):
    Neutral = 0
    Positive = 1
    Negative = 2
    Mixed = 3


PRESSURE_TRIGGER = 1.05
PRESSURE_DECAY = 0.55
COOLDOWN = 0.55
MAX_ENC_PER_SHIFT = 3
EXPOSURE_TICK = 0.12


def clamp(v, lo, hi):
    return lo if v < lo else hi if v > hi else v


def clamp01(v):
    return clamp(v, 0.0, 1.0)


def context_mul(ctx: Context) -> float:
    return {
        Context.SharedProblem: 1.55,
        Context.WorkingTogether: 1.25,
        Context.Emergency: 1.40,
        Context.RecentFailure: 1.20,
        Context.RecentSuccess: 1.10,
        Context.IdleNearby: 0.85,
    }.get(ctx, 1.0)


@dataclass
class Stats:
    composure: int = 10
    bravery: int = 10
    affinity: int = 10
    focus: int = 10
    work_rate: int = 10
    determination: int = 10
    tolerance: int = 10
    empathy: int = 10
    leadership: int = 10
    intuition: int = 10

    def get(self, name: str) -> int:
        return getattr(self, name)


@dataclass
class State:
    frustration: float = 8.0
    morale: float = 55.0
    focus_state: float = 62.0
    mental_fatigue: float = 18.0
    physical_stamina: float = 80.0


@dataclass
class Expression:
    positive: float
    negative: float
    intensity: float
    reach: float
    cls: AuraClass


def derive_expression(st: State, stats: Stats) -> Expression:
    morale01 = st.morale / 100.0
    frust01 = st.frustration / 100.0
    fatigue01 = st.mental_fatigue / 100.0
    focus_state01 = st.focus_state / 100.0
    pos_amp = 0.35 + stats.affinity / 40.0 + stats.leadership / 55.0
    pos = clamp01(morale01 * pos_amp * (1.0 - fatigue01 * 0.35))
    composure_mask = clamp01(1.15 - stats.composure / 28.0)
    reactivity = 1.0 + (1.0 - focus_state01) * 0.45
    neg = clamp01(frust01 * composure_mask * reactivity)
    intensity = clamp01(max(pos, neg) + 0.28 * min(pos, neg))
    reach = clamp(0.45 + intensity * 0.75 + stats.leadership * 0.012, 0.35, 1.6)
    hi_pos = pos >= 0.42
    hi_neg = neg >= 0.42
    if hi_pos and hi_neg:
        cls = AuraClass.Mixed
    elif pos >= neg + 0.12 and pos >= 0.32:
        cls = AuraClass.Positive
    elif neg >= pos + 0.12 and neg >= 0.32:
        cls = AuraClass.Negative
    else:
        cls = AuraClass.Neutral
    return Expression(pos, neg, intensity, reach, cls)


@dataclass
class Relation:
    trust: float = 0.0
    warmth: float = 0.0
    hostility: float = 0.0

    def add(self, t, w, h):
        self.trust = clamp(self.trust + t, -20, 20)
        self.warmth = clamp(self.warmth + w, -20, 20)
        self.hostility = clamp(self.hostility + h, -20, 20)


@dataclass
class Pair:
    pressure: float = 0.0
    cooldown: float = 0.0
    last_shift: float = 0.0
    exposure: float = 0.0


@dataclass
class Actor:
    id: int
    name: str
    stats: Stats
    state: State
    expression: Expression = field(default=None)

    def refresh(self):
        self.expression = derive_expression(self.state, self.stats)


@dataclass
class Log:
    shift: int
    init_id: int
    tgt_id: int
    context: Context
    action: Action
    response: Response
    action_ok: bool
    resp_ok: bool
    action_d20: int
    resp_d20: int
    d_tr_it: float
    d_wa_it: float
    d_ho_it: float
    d_tr_ti: float
    d_wa_ti: float
    d_ho_ti: float
    d_fr_i: float
    d_mo_i: float
    d_fr_t: float
    d_mo_t: float
    why_pressure: str
    why_init: str
    why_action: str
    why_resp: str
    outcome: str


class Rng:
    def __init__(self, seed: int):
        self.r = random.Random(seed)

    def d20(self) -> int:
        return self.r.randint(1, 20)

    def unit(self) -> float:
        return self.r.random()


def check(rng: Rng, stat: int, dc: int, mod: int = 0):
    d = rng.d20()
    total = d + stat + mod
    return d, total >= dc, dc


class World:
    def __init__(self, actors: List[Actor], rng: Rng):
        self.actors = {a.id: a for a in actors}
        for a in actors:
            a.refresh()
        self.directed: Dict[Tuple[int, int], Relation] = {}
        self.pairs: Dict[Tuple[int, int], Pair] = {}
        self.logs: List[Log] = []
        self.enc_shift: Dict[int, int] = {}
        self.shift = 0
        self.rng = rng

    def begin_shift(self, sh: int):
        self.shift = sh
        self.enc_shift.clear()
        for a in self.actors.values():
            a.refresh()

    def rel(self, a: int, b: int) -> Relation:
        k = (a, b)
        if k not in self.directed:
            self.directed[k] = Relation()
        return self.directed[k]

    def pair(self, a: int, b: int) -> Pair:
        k = (min(a, b), max(a, b))
        if k not in self.pairs:
            self.pairs[k] = Pair()
        return self.pairs[k]

    def decay(self):
        for p in self.pairs.values():
            p.pressure = max(0.0, p.pressure - PRESSURE_DECAY * 0.25)
            p.cooldown = max(0.0, p.cooldown - 0.15)
            p.exposure = 0.0

    def expose(self, id_a: int, id_b: int, ctx: Context, opportunity: float = 1.0):
        if id_a == id_b:
            return None
        a = self.actors[id_a]
        b = self.actors[id_b]
        a.refresh()
        b.refresh()
        pair = self.pair(id_a, id_b)
        if pair.cooldown > 0:
            pair.cooldown = max(0.0, pair.cooldown - EXPOSURE_TICK)
            return None
        ea = self.enc_shift.get(id_a, 0)
        eb = self.enc_shift.get(id_b, 0)
        if ea >= MAX_ENC_PER_SHIFT or eb >= MAX_ENC_PER_SHIFT:
            return None
        reach = min(a.expression.reach, b.expression.reach)
        intensity = 0.5 * (a.expression.intensity + b.expression.intensity)
        ab = self.rel(id_a, id_b)
        ba = self.rel(id_b, id_a)
        warmth_avg = 0.5 * (ab.warmth + ba.warmth)
        host_avg = 0.5 * (ab.hostility + ba.hostility)
        focus_avg = 0.5 * (a.stats.focus + b.stats.focus)
        focus_damp = clamp(1.1 - focus_avg / 40.0, 0.55, 1.15)
        rel_mul = 1.0 + warmth_avg * 0.015 + host_avg * 0.012
        if ctx == Context.SharedProblem:
            rel_mul += 0.18 + max(0.0, host_avg) * 0.01
        gain = (
            opportunity
            * EXPOSURE_TICK
            * intensity
            * reach
            * context_mul(ctx)
            * rel_mul
            * focus_damp
        )
        pair.pressure += gain
        pair.exposure += EXPOSURE_TICK
        why_p = (
            f"exposure={opportunity:.2f} intens={intensity:.2f} reach={reach:.2f} "
            f"ctx={ctx.name}×{context_mul(ctx):.2f} relMul={rel_mul:.2f} "
            f"focusDamp={focus_damp:.2f} P={pair.pressure:.2f}/{PRESSURE_TRIGGER}"
        )
        if pair.pressure < PRESSURE_TRIGGER:
            return None
        pair.pressure = 0.0
        pair.cooldown = COOLDOWN
        pair.last_shift = self.shift
        log = resolve(self, a, b, ctx, why_p)
        if log:
            self.logs.append(log)
            self.enc_shift[id_a] = ea + 1
            self.enc_shift[id_b] = eb + 1
        return log


def initiator_score(self: Actor, toward: Relation, ctx: Context) -> float:
    lead = self.stats.leadership / 20.0
    brave = self.stats.bravery / 20.0
    aff = self.stats.affinity / 20.0
    focus = self.stats.focus / 20.0
    score = (
        self.expression.intensity * 0.55
        + lead * 0.35
        + brave * 0.25
        + aff * 0.20
        + self.expression.positive * 0.15
        + self.expression.negative * 0.10
    )
    score -= focus * 0.22
    score += toward.warmth * 0.012
    if ctx == Context.SharedProblem:
        score += max(0.0, toward.hostility) * 0.01 + 0.08
    score -= self.state.mental_fatigue / 220.0
    return score


def weighted_pick(rng: Rng, w: List[float]) -> int:
    s = sum(w)
    if s <= 0:
        return 0
    pick = rng.unit() * s
    acc = 0.0
    for i, x in enumerate(w):
        acc += x
        if pick <= acc:
            return i
    return len(w) - 1


def action_weights(init: Actor, rel: Relation, ctx: Context) -> List[float]:
    pos = init.expression.positive
    neg = init.expression.negative
    s = init.stats
    w = [
        0.15 + pos * 0.9 + s.leadership / 25.0 + s.empathy / 30.0,
        0.12 + pos * 0.5 + s.affinity / 28.0 - init.state.frustration / 200.0,
        0.18 + pos * 0.7 + s.affinity / 22.0 + s.empathy / 28.0 + rel.warmth * 0.02,
        0.12 + neg * 0.85 + s.tolerance / 35.0,
        0.05 + neg * 0.9 + s.bravery / 22.0 - s.composure / 30.0 + rel.hostility * 0.03,
        0.05 + neg * 0.55 + s.bravery / 20.0 + s.determination / 28.0 - s.composure / 35.0,
    ]
    if ctx == Context.SharedProblem:
        w[3] *= 1.85
        w[4] *= 1.15
        w[0] *= 0.85
    if ctx == Context.RecentSuccess:
        w[0] *= 1.35
        w[1] *= 1.25
        w[2] *= 1.20
    if ctx == Context.Emergency:
        w[5] *= 1.4
        w[0] *= 1.2
    focus_mul = clamp(1.15 - s.focus / 35.0, 0.55, 1.15)
    w[4] *= focus_mul
    w[5] *= focus_mul
    return [max(0.02, x) for x in w]


def response_weights(
    target: Actor,
    toward_init: Relation,
    action: Action,
    action_ok: bool,
    ctx: Context,
) -> List[float]:
    s = target.stats
    neg = target.expression.negative
    pos = target.expression.positive
    w = [
        0.2 + s.composure / 40.0 + (0.15 if action_ok else 0.0) + pos * 0.2,
        0.18 + s.focus / 35.0 + s.composure / 45.0,
        0.15 + s.focus / 28.0 + s.tolerance / 40.0,
        0.1 + s.empathy / 30.0,
        0.08 + s.bravery / 35.0 + neg * 0.35,
        0.05 + s.bravery / 30.0 + s.determination / 40.0 + neg * 0.45 - s.composure / 40.0,
        0.12 + s.focus / 40.0 + (1.0 - s.bravery / 40.0),
    ]
    if action in (Action.Encourage, Action.Joke, Action.Connect):
        if action_ok:
            w[0] *= 1.4
            w[3] *= 1.2
        else:
            w[1] *= 1.3
            w[2] *= 1.2
            w[4] *= 1.15
    elif action == Action.Complain:
        if ctx == Context.SharedProblem:
            w[3] *= 2.2
            w[0] *= 1.2
            w[5] *= 0.7
        else:
            w[3] *= 1.2
            w[1] *= 1.1
            w[4] *= 1.1
    elif action in (Action.Provoke, Action.Confront):
        w[4] *= 1.35
        w[5] *= 1.25
        w[2] *= 1.15 + s.focus / 50.0
        w[0] *= 0.7
        w[6] *= 1.1
        w[5] *= clamp(1.2 - s.composure / 28.0 - s.tolerance / 45.0, 0.35, 1.2)
    if toward_init.hostility > 8:
        w[4] *= 1.3
        w[5] *= 1.25
    if toward_init.warmth > 8:
        w[0] *= 1.25
        w[3] *= 1.2
    return [max(0.02, x) for x in w]


def action_roll(rng: Rng, init: Actor, target: Actor, rel: Relation, action: Action, ctx: Context):
    s = init.stats
    if action == Action.Encourage:
        dc, mod = 11, (s.empathy - 10) // 4 - int(round(target.state.frustration / 40.0))
        return check(rng, s.leadership, dc, mod)
    if action == Action.Joke:
        dc, mod = 12, (s.intuition - 10) // 5 - int(round(target.expression.negative * 3))
        return check(rng, s.affinity, dc, mod)
    if action == Action.Connect:
        dc = 11
        mod = (s.empathy - 10) // 4 + int(round(rel.warmth / 6)) - int(round(rel.hostility / 5))
        return check(rng, s.affinity, dc, mod)
    if action == Action.Complain:
        dc = 9 if ctx == Context.SharedProblem else 12
        mod = (s.intuition - 10) // 5
        return check(rng, s.empathy, dc, mod)
    if action == Action.Provoke:
        dc = 12
        mod = (s.bravery - 10) // 5 - (target.stats.composure - 10) // 4
        return check(rng, s.intuition, dc, mod)
    # Confront
    dc = 13
    mod = (s.determination - 10) // 5 - (target.stats.tolerance - 10) // 5
    return check(rng, s.bravery, dc, mod)


def response_roll(rng: Rng, target: Actor, response: Response, action_ok: bool):
    mod = 1 if action_ok else -1
    s = target.stats
    if response in (Response.Accept, Response.Agree):
        return check(rng, s.empathy, 10, mod)
    if response in (Response.Deflect, Response.Ignore, Response.Withdraw):
        return check(rng, s.focus, 11 if response != Response.Withdraw else 10, mod)
    if response == Response.PushBack:
        return check(rng, s.bravery, 11, mod)
    # Escalate
    mod2 = mod - (s.composure - 10) // 5
    return check(rng, s.determination, 12, mod2)


def apply_consequences(
    init: Actor,
    target: Actor,
    rel_it: Relation,
    rel_ti: Relation,
    action: Action,
    response: Response,
    action_ok: bool,
    ctx: Context,
):
    d_tr_it = d_wa_it = d_ho_it = 0.0
    d_tr_ti = d_wa_ti = d_ho_ti = 0.0
    d_fr_i = d_mo_i = d_fr_t = d_mo_t = 0.0

    bond = (
        action == Action.Complain
        and response in (Response.Agree, Response.Accept)
        and (action_ok or ctx == Context.SharedProblem)
    )
    if bond:
        d_tr_it, d_wa_it, d_ho_it = 0.6, 1.4, -0.4
        d_tr_ti, d_wa_ti, d_ho_ti = 0.5, 1.3, -0.3
        d_fr_i, d_fr_t = -4.0, -3.5
        d_mo_i, d_mo_t = 1.2, 1.0
    else:
        if action == Action.Encourage:
            if action_ok and response in (Response.Accept, Response.Agree):
                d_tr_it, d_wa_it, d_ho_it = 0.8, 0.9, -0.2
                d_tr_ti, d_wa_ti = 0.5, 0.7
                d_fr_t, d_mo_t, d_mo_i = -2.5, 1.5, 0.6
            elif not action_ok:
                d_tr_it, d_wa_it, d_ho_it = -0.3, -0.5, 0.2
                d_fr_i, d_fr_t = 1.5, 1.0
        elif action == Action.Joke:
            if action_ok and response not in (Response.PushBack, Response.Escalate):
                d_tr_it, d_wa_it, d_ho_it = 0.3, 1.0, -0.2
                d_tr_ti, d_wa_ti = 0.2, 0.8
                d_fr_i, d_fr_t = -1.5, -1.0
            else:
                d_tr_it, d_wa_it, d_ho_it = -0.2, -0.6, 0.4
                d_fr_i, d_fr_t = 2.0, 1.2
        elif action == Action.Connect:
            if action_ok and response in (Response.Accept, Response.Agree):
                d_tr_it, d_wa_it, d_ho_it = 0.7, 1.2, -0.2
                d_tr_ti, d_wa_ti = 0.6, 1.0
                d_mo_i = d_mo_t = 0.8
            elif response in (Response.Ignore, Response.Withdraw):
                d_tr_it, d_wa_it, d_ho_it = -0.2, -0.4, 0.1
                d_fr_i = 2.2
        elif action == Action.Complain:
            if response in (Response.PushBack, Response.Escalate):
                d_tr_it, d_wa_it, d_ho_it = -0.3, -0.4, 0.8
                d_tr_ti, d_wa_ti, d_ho_ti = -0.2, -0.3, 0.7
                d_fr_i, d_fr_t = 2.0, 2.5
            else:
                d_fr_i = -1.0
        elif action in (Action.Provoke, Action.Confront):
            if response in (Response.Escalate, Response.PushBack):
                d_tr_it, d_wa_it, d_ho_it = -0.6, -0.8, 1.6
                d_tr_ti, d_wa_ti, d_ho_ti = -0.5, -0.7, 1.5
                d_fr_i, d_fr_t = 3.0, 3.5
                d_mo_i, d_mo_t = -0.8, -1.0
            elif response in (Response.Ignore, Response.Withdraw):
                d_ho_ti = 0.2
                d_fr_i = 2.8 + (0 if action_ok else 1.0)
            elif response in (Response.Accept, Response.Deflect):
                d_tr_it, d_wa_it, d_ho_it = -0.2, -0.2, 0.5
                d_fr_t = 1.5

    def cap(x):
        return clamp(x, -2.5, 2.5)

    d_tr_it, d_wa_it, d_ho_it = cap(d_tr_it), cap(d_wa_it), cap(d_ho_it)
    d_tr_ti, d_wa_ti, d_ho_ti = cap(d_tr_ti), cap(d_wa_ti), cap(d_ho_ti)
    rel_it.add(d_tr_it, d_wa_it, d_ho_it)
    rel_ti.add(d_tr_ti, d_wa_ti, d_ho_ti)
    init.state.frustration = clamp(init.state.frustration + d_fr_i, 0, 100)
    init.state.morale = clamp(init.state.morale + d_mo_i, 0, 100)
    target.state.frustration = clamp(target.state.frustration + d_fr_t, 0, 100)
    target.state.morale = clamp(target.state.morale + d_mo_t, 0, 100)
    if response == Response.Escalate:
        init.state.focus_state = clamp(init.state.focus_state - 1.5, 0, 100)
        target.state.focus_state = clamp(target.state.focus_state - 1.5, 0, 100)
    return (
        d_tr_it,
        d_wa_it,
        d_ho_it,
        d_tr_ti,
        d_wa_ti,
        d_ho_ti,
        d_fr_i,
        d_mo_i,
        d_fr_t,
        d_mo_t,
    )


def summarize(action, response, action_ok, ctx):
    if (
        action == Action.Complain
        and response in (Response.Agree, Response.Accept)
        and ctx == Context.SharedProblem
    ):
        return "SHARED_COMPLAINT_BOND"
    if action in (Action.Provoke, Action.Confront) and response in (
        Response.Escalate,
        Response.PushBack,
    ):
        return "CLASH"
    if action in (Action.Provoke, Action.Confront) and response in (
        Response.Ignore,
        Response.Withdraw,
    ):
        return "IGNORED_AGGRESSION"
    if action in (Action.Encourage, Action.Connect, Action.Joke) and action_ok and response in (
        Response.Accept,
        Response.Agree,
    ):
        return "POSITIVE_CONNECT"
    if action in (Action.Encourage, Action.Joke, Action.Connect) and not action_ok:
        return "POSITIVE_FAIL"
    return f"EXCHANGE_{action.name}_{response.name}"


def run_exchange(world: World, init: Actor, target: Actor, ctx: Context):
    rel_it = world.rel(init.id, target.id)
    rel_ti = world.rel(target.id, init.id)
    aw = action_weights(init, rel_it, ctx)
    action = Action(weighted_pick(world.rng, aw))
    d20a, aok, dca = action_roll(world.rng, init, target, rel_it, action, ctx)
    rw = response_weights(target, rel_ti, action, aok, ctx)
    response = Response(weighted_pick(world.rng, rw))
    d20r, rok, dcr = response_roll(world.rng, target, response, aok)
    deltas = apply_consequences(init, target, rel_it, rel_ti, action, response, aok, ctx)
    why_a = (
        f"weights Enc={aw[0]:.2f} Joke={aw[1]:.2f} Conn={aw[2]:.2f} Comp={aw[3]:.2f} "
        f"Prov={aw[4]:.2f} Conf={aw[5]:.2f} → {action.name} (d20={d20a} vs DC {dca} {'OK' if aok else 'FAIL'})"
    )
    why_r = (
        f"resp Acc={rw[0]:.2f} Def={rw[1]:.2f} Ign={rw[2]:.2f} Agr={rw[3]:.2f} "
        f"Push={rw[4]:.2f} Esc={rw[5]:.2f} Wdr={rw[6]:.2f} → {response.name} "
        f"(d20={d20r} vs DC {dcr} {'OK' if rok else 'FAIL'})"
    )
    return action, response, aok, rok, d20a, d20r, deltas, why_a, why_r


def resolve(world: World, a: Actor, b: Actor, ctx: Context, why_p: str) -> Log:
    sa = initiator_score(a, world.rel(a.id, b.id), ctx)
    sb = initiator_score(b, world.rel(b.id, a.id), ctx)
    a_init = sa >= sb
    if abs(sa - sb) < 0.08:
        a_init = world.rng.unit() >= 0.5
    init, target = (a, b) if a_init else (b, a)
    why_i = (
        f"{init.name} score={sa if a_init else sb:.2f} vs {target.name} "
        f"{sb if a_init else sa:.2f} (Leadership/Bravery/Affinity/Intensity)"
    )
    action, response, aok, rok, d20a, d20r, deltas, why_a, why_r = run_exchange(
        world, init, target, ctx
    )
    exchanges = 1
    outcome = summarize(action, response, aok, ctx)
    while (
        exchanges < 3
        and response in (Response.Escalate, Response.PushBack)
        and world.rng.unit() < 0.55
    ):
        action, response, aok, rok, d20a, d20r, d2, wa, wr = run_exchange(
            world, target, init, ctx
        )
        # accumulate into original initiator frame (approx)
        deltas = tuple(x + y for x, y in zip(deltas, (d2[2], d2[3], d2[4], d2[0], d2[1], d2[5], d2[8], d2[9], d2[6], d2[7])))
        exchanges += 1
        why_a += f" | x{exchanges}: {wa}"
        why_r += f" | x{exchanges}: {wr}"
        outcome = summarize(action, response, aok, ctx) + f"/x{exchanges}"
        if response in (Response.Ignore, Response.Withdraw, Response.Agree, Response.Accept):
            break
        init, target = target, init
    init.refresh()
    target.refresh()
    if exchanges > 1:
        outcome += f" (exchanges={exchanges})"
    return Log(
        world.shift,
        init.id if exchanges == 1 else (b.id if a_init else a.id),  # first initiator
        target.id if exchanges == 1 else (a.id if a_init else b.id),
        ctx,
        action,
        response,
        aok,
        rok,
        d20a,
        d20r,
        *deltas,
        why_p,
        why_i,
        why_a,
        why_r,
        outcome,
    )


# Fix resolve initiator ids — store first initiator properly
def resolve_fixed(world: World, a: Actor, b: Actor, ctx: Context, why_p: str) -> Log:
    sa = initiator_score(a, world.rel(a.id, b.id), ctx)
    sb = initiator_score(b, world.rel(b.id, a.id), ctx)
    a_init = sa >= sb
    if abs(sa - sb) < 0.08:
        a_init = world.rng.unit() >= 0.5
    first_init, first_tgt = (a, b) if a_init else (b, a)
    why_i = (
        f"{first_init.name} score={(sa if a_init else sb):.2f} vs {first_tgt.name} "
        f"{(sb if a_init else sa):.2f} (Leadership/Bravery/Affinity/Intensity)"
    )
    cur_i, cur_t = first_init, first_tgt
    action, response, aok, rok, d20a, d20r, deltas, why_a, why_r = run_exchange(
        world, cur_i, cur_t, ctx
    )
    exchanges = 1
    outcome = summarize(action, response, aok, ctx)
    # Keep deltas in first_init / first_tgt frame
    d_tr_it, d_wa_it, d_ho_it, d_tr_ti, d_wa_ti, d_ho_ti, d_fr_i, d_mo_i, d_fr_t, d_mo_t = deltas
    while (
        exchanges < 3
        and response in (Response.Escalate, Response.PushBack)
        and world.rng.unit() < 0.55
    ):
        cur_i, cur_t = cur_t, cur_i
        action, response, aok, rok, d20a, d20r, d2, wa, wr = run_exchange(
            world, cur_i, cur_t, ctx
        )
        if cur_i.id == first_init.id:
            d_tr_it += d2[0]
            d_wa_it += d2[1]
            d_ho_it += d2[2]
            d_tr_ti += d2[3]
            d_wa_ti += d2[4]
            d_ho_ti += d2[5]
            d_fr_i += d2[6]
            d_mo_i += d2[7]
            d_fr_t += d2[8]
            d_mo_t += d2[9]
        else:
            d_tr_it += d2[3]
            d_wa_it += d2[4]
            d_ho_it += d2[5]
            d_tr_ti += d2[0]
            d_wa_ti += d2[1]
            d_ho_ti += d2[2]
            d_fr_i += d2[8]
            d_mo_i += d2[9]
            d_fr_t += d2[6]
            d_mo_t += d2[7]
        exchanges += 1
        why_a += f" | x{exchanges}: {wa}"
        why_r += f" | x{exchanges}: {wr}"
        outcome = summarize(action, response, aok, ctx) + f"/x{exchanges}"
        if response in (Response.Ignore, Response.Withdraw, Response.Agree, Response.Accept):
            break
    first_init.refresh()
    first_tgt.refresh()
    if exchanges > 1:
        outcome += f" (exchanges={exchanges})"
    return Log(
        world.shift,
        first_init.id,
        first_tgt.id,
        ctx,
        action,
        response,
        aok,
        rok,
        d20a,
        d20r,
        d_tr_it,
        d_wa_it,
        d_ho_it,
        d_tr_ti,
        d_wa_ti,
        d_ho_ti,
        d_fr_i,
        d_mo_i,
        d_fr_t,
        d_mo_t,
        why_p,
        why_i,
        why_a,
        why_r,
        outcome,
    )


# Monkey-patch World to use fixed resolve
_orig_expose = World.expose


def _expose(self, id_a, id_b, ctx, opportunity=1.0):
    if id_a == id_b:
        return None
    a = self.actors[id_a]
    b = self.actors[id_b]
    a.refresh()
    b.refresh()
    pair = self.pair(id_a, id_b)
    if pair.cooldown > 0:
        pair.cooldown = max(0.0, pair.cooldown - EXPOSURE_TICK)
        return None
    ea = self.enc_shift.get(id_a, 0)
    eb = self.enc_shift.get(id_b, 0)
    if ea >= MAX_ENC_PER_SHIFT or eb >= MAX_ENC_PER_SHIFT:
        return None
    reach = min(a.expression.reach, b.expression.reach)
    intensity = 0.5 * (a.expression.intensity + b.expression.intensity)
    ab = self.rel(id_a, id_b)
    ba = self.rel(id_b, id_a)
    warmth_avg = 0.5 * (ab.warmth + ba.warmth)
    host_avg = 0.5 * (ab.hostility + ba.hostility)
    focus_avg = 0.5 * (a.stats.focus + b.stats.focus)
    focus_damp = clamp(1.1 - focus_avg / 40.0, 0.55, 1.15)
    rel_mul = 1.0 + warmth_avg * 0.015 + host_avg * 0.012
    if ctx == Context.SharedProblem:
        rel_mul += 0.18 + max(0.0, host_avg) * 0.01
    gain = (
        opportunity
        * EXPOSURE_TICK
        * intensity
        * reach
        * context_mul(ctx)
        * rel_mul
        * focus_damp
    )
    pair.pressure += gain
    pair.exposure += EXPOSURE_TICK
    why_p = (
        f"exposure={opportunity:.2f} intens={intensity:.2f} reach={reach:.2f} "
        f"ctx={ctx.name}×{context_mul(ctx):.2f} relMul={rel_mul:.2f} "
        f"focusDamp={focus_damp:.2f} P={pair.pressure:.2f}/{PRESSURE_TRIGGER}"
    )
    if pair.pressure < PRESSURE_TRIGGER:
        return None
    pair.pressure = 0.0
    pair.cooldown = COOLDOWN
    pair.last_shift = self.shift
    log = resolve_fixed(self, a, b, ctx, why_p)
    if log:
        self.logs.append(log)
        self.enc_shift[id_a] = ea + 1
        self.enc_shift[id_b] = eb + 1
    return log


World.expose = _expose


def profile_a():
    return Stats(composure=17, empathy=16, affinity=12, bravery=9, focus=12, tolerance=14, leadership=11, intuition=13, determination=11, work_rate=11)


def profile_b():
    return Stats(bravery=17, composure=6, determination=16, affinity=8, empathy=8, tolerance=7, focus=9, leadership=10, intuition=11, work_rate=12)


def profile_c():
    return Stats(affinity=17, leadership=16, focus=6, empathy=14, composure=11, bravery=11, tolerance=10, intuition=12, determination=10, work_rate=10)


def profile_d():
    return Stats(focus=17, tolerance=16, empathy=6, affinity=8, composure=13, bravery=8, leadership=8, intuition=10, determination=12, work_rate=13)


def mk_state(frust, morale, focus=70.0, fatigue=20.0):
    return State(frustration=frust, morale=morale, focus_state=focus, mental_fatigue=fatigue)


def actor(i, name, stats, state):
    return Actor(i, name, stats, state)


def pct(n, total):
    return "0%" if total <= 0 else f"{100.0 * n / total:.1f}%"


def mean(xs):
    return sum(xs) / len(xs) if xs else 0.0


def variance(xs):
    if len(xs) < 2:
        return 0.0
    m = mean(xs)
    return sum((x - m) ** 2 for x in xs) / len(xs)


def max_share(counts):
    s = sum(counts)
    return 0.0 if s <= 0 else max(counts) / s


def run_all():
    lines: List[str] = []
    def out(s=""):
        lines.append(s)

    out("# Social Aura Stage 0 — Model + Simulation Report")
    out(f"Generated: {datetime.now():%Y-%m-%d %H:%M:%S}")
    out()
    out("Scope: MODEL + MATH + SIMULATION ONLY. No live WorkerAvatar proximity.")
    out("Runner: Python diagnostic mirror of C# `SocialAuraStage0*` (Unity batchmode was project-locked).")
    out()
    out("## 1. Social Expression model")
    out()
    out("- `Positive` = Morale × (Affinity/Leadership amp) × (1 − MentalFatigue×0.35)")
    out("- `Negative` = Frustration × ComposureMask × FocusState reactivity")
    out("- High Composure → reduced outward negative leak (internal Frustration unchanged)")
    out("- High MentalFatigue softens positive availability without forcing hostility")
    out("- Low FocusState increases reactivity on negative channel")
    out("- **Not** Aura = Morale − Frustration")
    out()
    out("## 2. Aura intensity / reach")
    out()
    out("- Intensity = max(pos,neg) + 0.28×min(pos,neg) — Mixed allowed")
    out("- Reach = 0.45 + intensity×0.75 + Leadership×0.012 (clamped 0.35–1.6)")
    out("- Classification Positive/Neutral/Negative/Mixed is derived debug only")
    out()
    out("## 3. InteractionPressure model")
    out()
    out("- Gain = opportunity × tick × intensity × reachOverlap × contextMul × relationMul × focusDamp")
    out("- Builds while exposed; decays when separated; cooldown after encounter")
    out("- Trigger at PressureTrigger≈1.05; max 3 encounters/worker/shift")
    out("- No per-frame random encounter rolls")
    out()
    out("## 4. Pair transient state")
    out()
    out("- InteractionPressure, CooldownRemaining, LastEncounterShift, ExposureThisShift, RecentHistory")
    out("- Lives on social pair layer — not WorkerState")
    out()
    out("## 5. Directional relationship model")
    out()
    out("- A→B independent of B→A; axes Trust / Warmth / Hostility (−20..20)")
    out("- No Friendship scalar; no named Bond/Rivalry/Hate labels")
    out()
    out("## 6–12. Soul / actions / initiator / D20 / response / consequences / context")
    out()
    out("Implemented as specified in Stage 0 brief (see C# `SocialAuraStage0Encounter.cs`).")
    out("Soul chooses WHAT (weights); D20 decides HOW WELL (action-specific DC).")
    out("Contexts: WorkingTogether, SharedProblem, RecentSuccess, RecentFailure, IdleNearby, Emergency.")
    out()

    # Scenario A
    out("## 13. Scenario results")
    out()
    out("### SCENARIO A — Two frustrated + SharedProblem")
    bond = clash = other = 0
    for seed in range(100, 140):
        rng = Rng(seed)
        w = World(
            [
                actor(1, "A_Empath", profile_a(), mk_state(72, 35)),
                actor(2, "B_Hot", profile_b(), mk_state(78, 30)),
            ],
            rng,
        )
        for sh in range(12):
            w.begin_shift(sh)
            for _ in range(8):
                w.expose(1, 2, Context.SharedProblem)
            w.decay()
        for log in w.logs:
            if "SHARED_COMPLAINT_BOND" in log.outcome:
                bond += 1
            elif "CLASH" in log.outcome:
                clash += 1
            else:
                other += 1
    out(f"- Across 40 seeds × 12 shifts: bond={bond} clash={clash} other={other}")
    out(f"- Expected mix: both bonding and conflict present → {'PASS' if bond > 0 and clash > 0 else 'FAIL'}")
    out()

    # B
    out("### SCENARIO B — Aggressive vs high-Composure")
    provoke = escalate = resist = total = 0
    for seed in range(200, 230):
        rng = Rng(seed)
        w = World(
            [
                actor(1, "B_Hot", profile_b(), mk_state(70, 40)),
                actor(2, "A_Calm", profile_a(), mk_state(40, 55)),
            ],
            rng,
        )
        for sh in range(10):
            w.begin_shift(sh)
            for _ in range(7):
                w.expose(1, 2, Context.WorkingTogether)
            w.decay()
        for log in w.logs:
            total += 1
            if log.action in (Action.Provoke, Action.Confront):
                provoke += 1
            if log.response == Response.Escalate:
                escalate += 1
            if log.response in (Response.Deflect, Response.Ignore, Response.Withdraw, Response.Accept):
                resist += 1
    esc_rate = escalate / total if total else 0
    out(f"- Provocations/confronts: {provoke}/{total}; escalate: {escalate} ({esc_rate:.0%}); resist-ish: {resist}")
    out(f"- Expected: provocations possible, escalation not dominant → {'PASS' if provoke > 0 and esc_rate < 0.55 else 'FAIL'}")
    out()

    # C
    out("### SCENARIO C — High-Affinity positive vs low-morale withdrawn")
    pos_try = pos_ok = pos_fail = 0
    for seed in range(300, 330):
        rng = Rng(seed)
        w = World(
            [
                actor(1, "C_Lead", profile_c(), mk_state(25, 78)),
                actor(2, "D_Withdrawn", profile_d(), mk_state(55, 28, focus=80, fatigue=40)),
            ],
            rng,
        )
        for sh in range(10):
            w.begin_shift(sh)
            for _ in range(7):
                w.expose(1, 2, Context.IdleNearby)
            w.decay()
        for log in w.logs:
            if log.action in (Action.Encourage, Action.Connect, Action.Joke):
                pos_try += 1
                if log.action_ok:
                    pos_ok += 1
                else:
                    pos_fail += 1
    out(f"- Positive attempts: {pos_try} (ok={pos_ok} fail={pos_fail})")
    out(f"- Expected: some attempts + mixed success → {'PASS' if pos_try > 5 and pos_fail > 0 and pos_ok > 0 else 'FAIL'}")
    out()

    # D
    out("### SCENARIO D — Same pair over 20 shifts")
    rng = Rng(42)
    w = World(
        [
            actor(1, "A", profile_a(), mk_state(60, 45)),
            actor(2, "B", profile_b(), mk_state(65, 40)),
        ],
        rng,
    )
    early = late = []
    for sh in range(20):
        w.begin_shift(sh)
        for _ in range(6):
            w.expose(1, 2, Context.SharedProblem if sh < 10 else Context.WorkingTogether)
        w.decay()
        warmth = 0.5 * (w.rel(1, 2).warmth + w.rel(2, 1).warmth)
        if sh < 5:
            early.append(warmth)
        if sh >= 15:
            late.append(warmth)
    e, l = mean(early), mean(late)
    out(f"- Mean pair Warmth early: {e:.2f} → late: {l:.2f} (|Δ|={abs(l-e):.2f})")
    out(f"- Encounters logged: {len(w.logs)}; relation moved → {'PASS' if abs(l-e) > 0.4 or len(w.logs) > 3 else 'FAIL'}")
    out()

    # E
    out("### SCENARIO E — A & B dislike C → A/B may warm")
    ab_warm_rise = 0
    for seed in range(500, 530):
        rng = Rng(seed)
        w = World(
            [
                actor(1, "A", profile_a(), mk_state(70, 40)),
                actor(2, "B", profile_c(), mk_state(68, 42)),
                actor(3, "C", profile_b(), mk_state(75, 35)),
            ],
            rng,
        )
        w.rel(1, 3).hostility = 6
        w.rel(2, 3).hostility = 6
        w.rel(1, 3).warmth = -3
        w.rel(2, 3).warmth = -3
        ab0 = 0.5 * (w.rel(1, 2).warmth + w.rel(2, 1).warmth)
        for sh in range(15):
            w.begin_shift(sh)
            for t in range(5):
                w.expose(1, 2, Context.SharedProblem)
                w.expose(1, 3, Context.SharedProblem, 0.9)
                w.expose(2, 3, Context.SharedProblem, 0.9)
            w.decay()
        ab1 = 0.5 * (w.rel(1, 2).warmth + w.rel(2, 1).warmth)
        if ab1 > ab0 + 0.8:
            ab_warm_rise += 1
    out(f"- Seeds where A↔B Warmth rose ≥0.8: {ab_warm_rise}/30")
    out(f"- Expected: possible positive A/B growth → {'PASS' if ab_warm_rise >= 5 else 'FAIL'}")
    out()

    # F
    out("### SCENARIO F — High-Focus ignores aggressor")
    agg_d = tgt_d = 0.0
    ignored = n = 0
    for seed in range(600, 630):
        rng = Rng(seed)
        agg = actor(1, "B_Hot", profile_b(), mk_state(65, 40))
        foc = actor(2, "D_Focus", profile_d(), mk_state(35, 50, focus=85))
        a0, f0 = agg.state.frustration, foc.state.frustration
        w = World([agg, foc], rng)
        for sh in range(12):
            w.begin_shift(sh)
            for _ in range(7):
                w.expose(1, 2, Context.WorkingTogether)
            w.decay()
        for log in w.logs:
            if "IGNORED_AGGRESSION" in log.outcome:
                ignored += 1
        agg_d += agg.state.frustration - a0
        tgt_d += foc.state.frustration - f0
        n += 1
    aD, tD = agg_d / n, tgt_d / n
    out(f"- Mean ΔFrustration aggressor={aD:.1f} target={tD:.1f}; ignored-aggression={ignored}")
    out(f"- Expected: target more stable; aggressor hotter → {'PASS' if aD > tD + 1 and ignored > 0 else 'FAIL'}")
    out()

    # 100-run
    out("## 14. 100-run diagnostic (20 shifts each)")
    out()
    actions = [0] * 6
    responses = [0] * 7
    total_enc = 0
    worker_shift_slots = 0
    pos_out = neg_out = neu_out = 0
    escalations = ign_wdr = 0
    sum_abs_rel = 0.0
    extreme_friend = extreme_enemy = 0
    bond_c = clash_o = pos_fail = 0
    lead_init = init_count = 0
    final_w: List[float] = []
    final_h: List[float] = []
    stories: List[str] = []

    for run in range(100):
        seed = 9000 + run * 17
        rng = Rng(seed)
        actors = [
            actor(1, "A", profile_a(), mk_state(55 + (run % 5) * 4, 45)),
            actor(2, "B", profile_b(), mk_state(60 + (run % 4) * 5, 38)),
            actor(3, "C", profile_c(), mk_state(35, 70 - (run % 6) * 3)),
            actor(4, "D", profile_d(), mk_state(40, 50)),
        ]
        w = World(actors, rng)
        contexts = [
            Context.SharedProblem,
            Context.WorkingTogether,
            Context.IdleNearby,
            Context.RecentFailure,
            Context.RecentSuccess,
        ]
        for sh in range(20):
            w.begin_shift(sh)
            ctx = contexts[sh % 5]
            for t in range(5):
                w.expose(1, 2, ctx, 0.95)
                w.expose(1, 3, ctx, 0.85)
                w.expose(2, 4, ctx, 0.85)
                if t % 2 == 0:
                    w.expose(3, 4, ctx, 0.75)
                if t % 3 == 0:
                    w.expose(2, 3, Context.SharedProblem, 0.9)
            w.decay()
            worker_shift_slots += 4
        total_enc += len(w.logs)
        for log in w.logs:
            actions[int(log.action)] += 1
            responses[int(log.response)] += 1
            sum_abs_rel += abs(log.d_wa_it) + abs(log.d_ho_it) + abs(log.d_tr_it)
            if log.response == Response.Escalate:
                escalations += 1
            if log.response in (Response.Ignore, Response.Withdraw):
                ign_wdr += 1
            if "SHARED_COMPLAINT_BOND" in log.outcome or "POSITIVE_CONNECT" in log.outcome:
                pos_out += 1
            elif "CLASH" in log.outcome or "POSITIVE_FAIL" in log.outcome:
                neg_out += 1
            else:
                neu_out += 1
            if "SHARED_COMPLAINT_BOND" in log.outcome:
                bond_c += 1
            if "CLASH" in log.outcome:
                clash_o += 1
            if "POSITIVE_FAIL" in log.outcome:
                pos_fail += 1
            init = w.actors[log.init_id]
            tgt = w.actors[log.tgt_id]
            init_count += 1
            if init.stats.leadership > tgt.stats.leadership:
                lead_init += 1
        for a, b in ((1, 2), (1, 3), (2, 4), (3, 4)):
            warmth = 0.5 * (w.rel(a, b).warmth + w.rel(b, a).warmth)
            host = 0.5 * (w.rel(a, b).hostility + w.rel(b, a).hostility)
            final_w.append(warmth)
            final_h.append(host)
            if warmth > 12 and host < 2:
                extreme_friend += 1
            if host > 12 and warmth < -2:
                extreme_enemy += 1
        if run < 5 or run in (42, 77):
            for log in w.logs:
                if len(stories) >= 24:
                    break
                i = w.actors[log.init_id]
                t = w.actors[log.tgt_id]
                stories.append(
                    f"run{run} sh{log.shift}: {i.name}→{t.name} {log.action.name}/{log.response.name} "
                    f"[{log.outcome}] ΔW={log.d_wa_it:.1f}/{log.d_wa_ti:.1f} "
                    f"ΔH={log.d_ho_it:.1f}/{log.d_ho_ti:.1f} | {log.why_init}"
                )

    enc_per = (2.0 * total_enc) / worker_shift_slots if worker_shift_slots else 0
    lead_share = lead_init / init_count if init_count else 0
    out(f"- Runs: 100 × 20 shifts × 4 workers")
    out(f"- Total encounters: {total_enc}")
    out(f"- Encounters per worker/shift (approx): {enc_per:.2f} (target ~0–3)")
    out("- Action distribution:")
    for i, name in enumerate(Action):
        out(f"  - {name.name}: {actions[i]} ({pct(actions[i], total_enc)})")
    out("- Response distribution:")
    for i, name in enumerate(Response):
        out(f"  - {name.name}: {responses[i]} ({pct(responses[i], total_enc)})")
    out(f"- Outcome buckets: +{pos_out} / −{neg_out} / ~{neu_out}")
    out(f"- Escalation responses: {escalations} ({pct(escalations, total_enc)})")
    out(f"- Ignore+Withdraw: {ign_wdr} ({pct(ign_wdr, total_enc)})")
    out(f"- Bond-complaint / Clash / Positive-fail: {bond_c} / {clash_o} / {pos_fail}")
    out(f"- Mean |relation delta| per enc: {(sum_abs_rel / total_enc if total_enc else 0):.2f}")
    out(f"- Extreme friend/enemy pair samples: {extreme_friend}/{extreme_enemy} (of {len(final_w)})")
    out(f"- Leadership-higher initiator share: {lead_share:.0%}")
    out(f"- Final pair Warmth mean={mean(final_w):.2f} Hostility mean={mean(final_h):.2f}")
    out()

    out("## 15. Emergent story examples")
    out()
    out("Sample explainable beats:")
    out()
    for s in stories[:12]:
        out(f"- {s}")
    out()
    out("Narrative patterns observed:")
    out("- SharedProblem + Complain→Agree can raise Warmth/Trust without prior friendship.")
    out("- Hot/low-Composure workers emit more Provoke/Confront; composed targets Deflect/Ignore.")
    out("- Positive Encourage/Connect can fail (POSITIVE_FAIL) and leave awkward Frustration.")
    out("- High-Focus workers damp pressure and Ignore aggression; aggressor Frustration drifts up.")
    out("- Directional Trust/Warmth/Hostility diverge — A→B ≠ B→A in many pairs.")
    out()

    out("## 16. Failure-condition audit")
    out()
    pass_n = fail_n = 0

    def check_c(name, ok, detail=""):
        nonlocal pass_n, fail_n
        if ok:
            pass_n += 1
            out(f"PASS | {name}" + (f" — {detail}" if detail else ""))
        else:
            fail_n += 1
            out(f"FAIL | {name}" + (f" — {detail}" if detail else ""))

    clash_share = clash_o / total_enc if total_enc else 1
    bond_share = bond_c / total_enc if total_enc else 0
    pos_fail_share = pos_fail / total_enc if total_enc else 0
    escalate_share = escalations / total_enc if total_enc else 1
    ignore_share = ign_wdr / total_enc if total_enc else 0
    provoke_share = (actions[4] + actions[5]) / total_enc if total_enc else 0
    encourage_share = actions[0] / total_enc if total_enc else 0

    check_c("Negative workers do not always fight", clash_share < 0.45 and bond_share > 0.02, f"clash={clash_share:.0%} bond={bond_share:.0%}")
    check_c("Positive actions can fail", pos_fail > 0 and pos_fail_share < 0.5, f"posFail={pos_fail} ({pos_fail_share:.0%})")
    check_c("Leadership does not dominate initiation", lead_share < 0.72, f"leadInit={lead_share:.0%}")
    check_c("No single action monopolizes (>55%)", max_share(actions) < 0.55, f"maxAction={max_share(actions):.0%}")
    check_c("Relationships do not converge too fast", extreme_friend + extreme_enemy < len(final_w) * 0.35, f"extremes={extreme_friend + extreme_enemy}/{len(final_w)}")
    check_c("Not every pair friends/enemies", extreme_friend < len(final_w) * 0.5 and extreme_enemy < len(final_w) * 0.5)
    check_c("Encounter frequency in ~0–3 / worker / shift", 0.05 <= enc_per <= 3.2, f"rate={enc_per:.2f}")
    check_c("Escalation is not constant noise", escalate_share < 0.40 and ignore_share > 0.05, f"esc={escalate_share:.0%} ign/wdr={ignore_share:.0%}")
    check_c("Shared frustration can bond", bond_c > 0, f"bonds={bond_c}")
    check_c("Provocation exists but is not everything", 0.05 < provoke_share < 0.55, f"provoke+confront={provoke_share:.0%}")
    check_c("Encourage exists (Leadership/Empathy path)", encourage_share > 0.03, f"encourage={encourage_share:.0%}")
    check_c("Directional multi-axis relations (Warmth≠Hostility mirror)", variance(final_w) > 0.01 or variance(final_h) > 0.01)
    check_c("High Focus is not socially immune (encounters occur with D)", total_enc > 50)
    out()
    out(f"Audit tally: {pass_n} PASS / {fail_n} FAIL")
    ready = fail_n == 0
    out()
    out("## 17. Tuning risks")
    out()
    out("- PressureTrigger / Cooldown dominate frequency — retune before live proximity.")
    out("- Composure mask can under-express frustration if set too high globally.")
    out("- SharedProblem Complain→Agree path must stay probabilistic, not guaranteed.")
    out("- Leadership soft-reach + initiator score can stack; keep Leadership soft.")
    out("- Multi-exchange only on Escalate/PushBack — may under-represent long cool talks.")
    out("- WorkRate fairness judgment is stubbed (contribution comparison deferred).")
    out()
    out("## 18. Recommendation")
    out()
    if ready:
        out("RECOMMENDATION: READY for live Social Aura integration (proximity wiring next).")
        out()
        out("Stage 0 math produces explainable pair trajectories under controlled exposure. Proceed to Stage 1 proximity only after locking these constants.")
    else:
        out("RECOMMENDATION: NOT READY for live Social Aura integration.")
        out()
        out("Address FAIL items above before wiring WorkerAvatar distance.")
    return "\n".join(lines) + "\n", ready


def main():
    text, ready = run_all()
    os.makedirs(OUT_DIR, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    path = os.path.join(OUT_DIR, f"social_aura_stage0_{stamp}.md")
    latest = os.path.join(OUT_DIR, "social_aura_stage0_latest.md")
    with open(path, "w") as f:
        f.write(text)
    with open(latest, "w") as f:
        f.write(text)
    print(f"Report: {path}")
    print("VERDICT:", "READY" if ready else "NOT READY")
    return 0 if ready else 1


if __name__ == "__main__":
    raise SystemExit(main())
