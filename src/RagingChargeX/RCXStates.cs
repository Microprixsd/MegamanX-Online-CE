﻿using System;
using System.Collections.Generic;
using System.Text;
using SFML.Graphics;

namespace MMXOnline;

public class RcxState : CharState {
	public RagingChargeX mmx = null!;

	public RcxState(
		string sprite, string shootSprite = "", string attackSprite = "",
		string transitionSprite = "", string transShootSprite = ""
	) : base(
		sprite, shootSprite, attackSprite, transitionSprite, transShootSprite
	) {
	}

	public override void onEnter(CharState oldState) {
		mmx = character as RagingChargeX ?? throw new NullReferenceException();
		base.onEnter(oldState);
	}
}

public class RCXParryStartState : CharState {
	public RCXParryStartState() : base("unpo_parry_start") {
		superArmor = true;
		stunResistant = true;
		immuneToWind = true;
		invincible = true;
	}

	public override void update() {
		base.update();
		if (stateFrames == 0) {
			character.turnToInput(player.input, player);
		}
		if (character.isAnimOver()) {
			character.changeToIdleOrFall();
		}
	}

}
public class XUPParryStartState : CharState {
	public RagingChargeX mmx = null!;
	public XUPParryStartState() : base("unpo_parry_start") {
	}

	public override void update() {
		base.update();

		if (stateTime < 0.1f) {
			character.turnToInput(player.input, player);
		}

		if (character.isAnimOver()) {
			character.changeToIdleOrFall();
		}
	}

	public void counterAttack(Player? damagingPlayer, Actor? damagingActor, float damage) {
		Actor? counterAttackTarget = null;
		Projectile? absorbedProj = null;

		if (player.weapon is XBuster { isUnpoBuster: true }) {
			player.weapon.ammo = player.weapon.maxAmmo;
		}

		if (damagingActor is Projectile proj) {
			if (proj.owningActor != null) {
				counterAttackTarget = proj.owningActor;
			}
			if (!proj.isMelee && proj.shouldVortexSuck) {
				absorbedProj = proj;
				absorbedProj.destroySelfNoEffect(doRpcEvenIfNotOwned: true);
			}
		}

		if (absorbedProj != null) {
			if (character.ownedByLocalPlayer) {
				bool shootProj = false;
				bool absorbThenShoot = false;
				character.playSound("upParryAbsorb", sendRpc: true);
				if (!player.input.isWeaponLeftOrRightHeld(player)) {
					mmx.absorbedProj = absorbedProj;
					//character.player.weapons.Add(new AbsorbWeapon(absorbedProj));
				} else {
					shootProj = true;
					absorbThenShoot = true;
				}
				//mmx.refillUnpoBuster();
				character.changeState(new XUPParryProjState(absorbedProj, shootProj, absorbThenShoot), true);
			}

			return;
		}

		if (counterAttackTarget == null) {
			counterAttackTarget = damagingPlayer?.character ?? damagingActor;
		}

		if (counterAttackTarget != null && character.pos.distanceTo(counterAttackTarget.pos) < 75 && counterAttackTarget is Character chr) {
			if (!chr.ownedByLocalPlayer) {
				RPC.actorToggle.sendRpc(chr.netId, RPCActorToggleType.ChangeToParriedState);
			} else {
				chr.changeState(new ParriedState(), true);
			}
		}
		mmx.addPercentAmmo(100);
		character.playSound("upParry", sendRpc: true);
		character.changeState(new XUPParryMeleeState(counterAttackTarget, damage), true);
	}

	public bool canParry() {
		return character.frameIndex == 0;
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		mmx = player.character as RagingChargeX ?? throw new NullReferenceException();
	}

	public override void onExit(CharState? newState) {
		base.onExit(newState);
	}
}

public class ParriedState : CharState {
	public ParriedState() : base("grabbed") {
	}

	public override bool canEnter(Character character) {
		if (!base.canEnter(character)) return false;
		if (character.charState is ParriedState) return false;
		if (character.isInvulnerable()) return false;
		if (character.charState.invincible) return false;
		return true;
	}

	public override void update() {
		base.update();
		if (stateTime > 0.5f) {
			character.changeToIdleOrFall();
		}
	}
}

public class UPParryMeleeProj : Projectile {
	public UPParryMeleeProj(
		Point pos, int xDir, float damage, Actor owner, Player player, ushort? netId, bool rpc = false
	) : base(
		pos, xDir, owner, "mmx_unpo_parry_proj", netId, player
	) {
		weapon = RCXParry.netWeapon;
		damager.damage = damage;
		damager.hitCooldown = 30;
		damager.flinch = Global.defFlinch;
		vel = new Point(0 * xDir, 0);
		projId = (int)ProjIds.UPParryMelee;
		setIndestructableProperties();
		maxTime = 0.25f;

		if (rpc) {
			rpcCreate(pos, owner, ownerPlayer, netId, xDir, (byte)damage);
		}
	}

	public override void onStart() {
		base.onStart();
		new Anim(pos.addxy(-10, 0), "explosion", xDir, null, true);
		new Anim(pos.addxy(10, 0), "explosion", xDir, null, true);
	}

	public static Projectile rpcInvoke(ProjParameters args) {
		return new UPParryMeleeProj(
			args.pos, args.xDir, args.extraData[0], args.owner, args.player, args.netId
		);
	}
}

public class XUPParryMeleeState : CharState {
	Actor counterAttackTarget;
	float damage;
	public RagingChargeX mmx = null!;
	public XUPParryMeleeState(Actor counterAttackTarget, float damage) : base("unpo_parry_attack") {
		invincible = true;
		this.counterAttackTarget = counterAttackTarget;
		this.damage = damage;
	}

	public override void update() {
		base.update();
		if (counterAttackTarget != null) {
			character.turnToPos(counterAttackTarget.pos);
			float dist = character.pos.distanceTo(counterAttackTarget.pos);
			if (dist < 150) {
				if (character.frameIndex >= 4 && !once) {
					if (character.pos.distanceTo(counterAttackTarget.pos) > 10) {
						character.moveToPos(counterAttackTarget.pos, 350);
					}
				}
			}
		}
		Point? shootPos = character.getFirstPOI("melee");
		if (!once && shootPos != null) {
			once = true;
			new UPParryMeleeProj(shootPos.Value, character.xDir, damage,
			mmx, player, player.getNextActorNetId(), rpc: true);
			character.playSound("upParryAttack", sendRpc: true);
			character.shakeCamera(sendRpc: true);
		}
		if (character.isAnimOver()) {
			character.changeToIdleOrFall();
		}

	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		mmx = player.character as RagingChargeX ?? throw new NullReferenceException();
		//character.frameIndex = 2;
	}

	public override void onExit(CharState? newState) {
		base.onExit(newState);
		if (character is RagingChargeX rcx) {
			rcx.parryCooldown = 0;
		}
	}
}


public class UPParryRangedProj : Projectile {
	public UPParryRangedProj(
		Point pos, int xDir, string sprite, float damage, int flinch, float hitCooldown,
		Actor owner, Player player, ushort? netId, bool rpc = false
	) : base(
		pos, xDir, owner, sprite, netId, player
	) {
		weapon = RCXParry.netWeapon;
		damager.damage = damage;
		damager.hitCooldown = hitCooldown;
		damager.flinch = flinch;
		vel = new Point(350 * xDir, 0);
		projId = (int)ProjIds.UPParryProj;
		maxDistance = 150;
		damager.damage = MathInt.Ceiling(damage);

		if (rpc) {
			List<Byte> extraBytes = new List<Byte> {
				(byte)damager.damage,
				(byte)damager.flinch
			};
			extraBytes.AddRange(BitConverter.GetBytes(hitCooldown));
			extraBytes.AddRange(Encoding.ASCII.GetBytes(sprite));
			rpcCreate(pos, owner, ownerPlayer, netId, xDir, extraBytes.ToArray());

		}
	}

	public static Projectile rpcInvoke(ProjParameters args) {
		float hitCooldown = BitConverter.ToUInt16(args.extraData[2..6], 0);
		string sprite = Encoding.ASCII.GetString(args.extraData[6..]);

		return new UPParryRangedProj(
			args.pos, args.xDir, sprite, args.extraData[0],
			args.extraData[1], hitCooldown, args.owner, args.player, args.netId
		);
	}
}

public class XUPParryProjState : CharState {
	Projectile? otherProj;
	Anim? absorbAnim;
	bool shootProj;
	bool absorbThenShoot;
	public RagingChargeX mmx = null!;
	public XUPParryProjState(Projectile otherProj, bool shootProj, bool absorbThenShoot) : base("unpo_parry_attack") {
		this.otherProj = otherProj;
		invincible = true;
		this.shootProj = shootProj;
		this.absorbThenShoot = absorbThenShoot;
	}

	public override void update() {
		base.update();
		if (!shootProj && character.sprite.frameIndex >= 1) {
			character.sprite.frameIndex = 1;
			character.sprite.frameSpeed = 0;
		}
		if (absorbAnim != null) {
			absorbAnim.moveToPos(character.getFirstPOIOrDefault(), 350);
			absorbAnim.xScale -= Global.spf * 5;
			absorbAnim.yScale -= Global.spf * 5;
			if (absorbAnim.xScale <= 0) {
				absorbAnim.destroySelf();
				absorbAnim = null;
				if (!shootProj) {
					character.changeToIdleOrFall();
					return;
				}
			}
		}
		Point? shootPos = character.getFirstPOI("proj");
		if (!once && shootPos != null) {
			once = true;
			float damage = Math.Max(otherProj.damager.damage * 2, 4);
			//int flinch = otherProj.damager.flinch;
			int flinch = Global.defFlinch;
			float hitCooldown = otherProj.damager.hitCooldownSeconds;
			new UPParryRangedProj(
				shootPos.Value, character.xDir,
				otherProj.sprite.name, damage, flinch, hitCooldown,
				mmx, player, player.getNextActorNetId(), rpc: true
			);
		}
		if (character.isAnimOver()) {
			character.changeToIdleOrFall();
		}
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		mmx = player.character as RagingChargeX ?? throw new NullReferenceException();
		if (!shootProj || absorbThenShoot) {
			absorbAnim = new Anim(
				otherProj.pos, otherProj.sprite.name, otherProj.xDir,
				player.getNextActorNetId(), false, sendRpc: true
			);
			absorbAnim.syncScale = true;
		}
	}

	public override void onExit(CharState? newState) {
		base.onExit(newState);
		if (character is RagingChargeX rcx) {
			rcx.parryCooldown = 0;
		}
		absorbAnim?.destroySelf();
	}
}


public class XUPPunchState : CharState {
	float slideVelX;
	bool isGrounded;
	public XUPPunchState(bool isGrounded) : base(isGrounded ? "unpo_punch" : "unpo_air_punch") {
		this.isGrounded = isGrounded;
		landSprite = "unpo_punch";
		airMove = true;
		useDashJumpSpeed = true;
	}

	public override void update() {
		base.update();
		character.move(new Point(slideVelX, 0));
		slideVelX = Helpers.lerp(slideVelX, 0, Global.spf * 5);

		if (character.isAnimOver() || (!isGrounded && character.grounded)) {
			character.changeToIdleOrFall();
		}
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		if (oldState is Dash) {
			slideVelX = character.xDir * 250;
		}
	}
}

public class XUPGrabState : CharState {
	public Character? victim;
	float leechTime = 200;
	public bool victimWasGrabbedSpriteOnce;
	float timeWaiting;
	float damageTime = 200;

	public XUPGrabState(Character? victim) : base("unpo_grab") {
		this.victim = victim;
		grabTime = 150;
	}

	public override void update() {
		base.update();
		grabTime -= Global.speedMul;
		leechTime += Global.speedMul;
		damageTime += Global.speedMul;

		if (victimWasGrabbedSpriteOnce && !victim.sprite.name.EndsWith("_grabbed")) {
			character.changeToIdleOrFall();
			return;
		}

		if (victim.sprite.name.EndsWith("_grabbed") || victim.sprite.name.EndsWith("_die")) {
			// Consider a max timer of 0.5-1 second here before the move just shorts out. Same with other command grabs
			victimWasGrabbedSpriteOnce = true;
		}
		if (!victimWasGrabbedSpriteOnce) {
			timeWaiting += Global.spf;
			if (timeWaiting > 1) {
				victimWasGrabbedSpriteOnce = true;
			}
			if (character.isDefenderFavored()) {
				if (leechTime >= 20) {
					leechTime = 0;
					character.addHealth(1);
					character.playSound("heal", true);
				}
				return;
			}
		}

		if (character.sprite.name.Contains("unpo_grab")) {
			Point enemyHeadPos = victim.getHeadPos() ?? victim.getCenterPos().addxy(0, -10);
			Point poi = character.getFirstPOIOffsetOnly() ?? new Point();

			Point snapPos = enemyHeadPos.addxy(-poi.x * character.xDir, -poi.y);

			character.changePos(Point.lerp(character.pos, snapPos, 0.25f));

			if (!character.grounded && !character.sprite.name.EndsWith("2")) {
				character.changeSpriteFromName("unpo_grab2", true);
			}
		}

		if (leechTime >= 20) {
			leechTime = 0;
			character.addHealth(1);
			Damager damager = new Damager(player, 1, 0, 0);
			if (damageTime >= 40) {
				damageTime = 0;
				damager.applyDamage(
					victim, false, new RCXGrab(), character, (int)ProjIds.UPGrabDamage
				);
				character.playSound("hit", true, true);
			} else {
				character.playSound("heal", true);
			}
		}

		if (player.input.isPressed(Control.Special1, player)) {
			character.changeToIdleOrFall();
			return;
		}
		if (player.input.isPressed(Control.WeaponRight, player)) {
			character.changeState(new XUPParryStartState(), true);
			return;
		}

		if (grabTime <= 0) {
			character.changeToIdleOrFall();
			return;
		}
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		character.useGravity = false;
	}

	public override void onExit(CharState? newState) {
		base.onExit(newState);
		character.useGravity = true;
		//character.grabCooldown = 1;
		if (victim != null) {
			victim.grabInvulnTime = 1;
			victim?.releaseGrab(character);
		}
	}
}

public class UPGrabbed : CharState {
	public const float maxGrabTime = 4;
	public Character? grabber;
	public long savedZIndex;
	public UPGrabbed(Character? grabber) : base("grabbed") {
		this.grabber = grabber;
	}

	public override bool canEnter(Character character) {
		if (!base.canEnter(character)) return false;
		return !character.isInvulnerable() && !character.charState.invincible;
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		character.stopMoving();
		character.stopCharge();
		savedZIndex = character.zIndex;
		character.setzIndex(grabber.zIndex - 100);
	}

	public override void onExit(CharState? newState) {
		base.onExit(newState);
		character.grabInvulnTime = 2;
		character.setzIndex(savedZIndex);
	}

	public override void update() {
		base.update();

		grabTime -= player.mashValue();
		if (grabTime <= 0) {
			character.changeToIdleOrFall();
			character.setHurt(-character.xDir, Global.defFlinch, false);
		}
	}
}

public class XReviveStart : CharState {
	public float boxHeight;
	public float boxOffset = 30;
	const float boxSpeed = 90;
	public string dialogLine1Content = "X, I want you want you to figth";
	public string dialogLine2Content = "for the people of the future.";
	public string dialogLine3Content = "You are the world one true hope.";
	public int dialogType;
	public float dialogTime;

	public string dialogLine1 = "";
	public string dialogLine2 = "";
	int frameCount;
	public int state;
	int dialogIndex;
	float subStateTime;
	Anim drLightAnim;

	RagingChargeX mmx;

	public XReviveStart() : base("revive_start") {
		invincible = true;
	}

	public bool cancellable() {
		if (stateTime > 1.8f) return true;
		//if (state >= 2) return true;
		//if (state == 2 && subStateTime > 0.5f) return true;
		return false;
	}

	public override void update() {
		base.update();

		if (cancellable() && Global.input.isPressed(Control.Special1, player)) {
			character.changeState(new XRevive(), true);
			return;
		}

		if (drLightAnim != null && drLightAnim.isAnimOver() && drLightAnim.sprite.name == "drlight") {
			drLightAnim.changeSprite("drlight_talk", true);
		}

		if (state == 0) {
			boxHeight += Global.spf * boxSpeed;
			boxOffset -= Global.spf * boxSpeed * 0.5f;
			if (boxHeight > 60) {
				state = 1;
				frameCount = 0;
				boxOffset = 0;
				boxHeight = 60;
			}
		} else if (state == 1) {
			drLightAnim.frameSpeed = 1;
			dialogTime += Global.spf;

			float dialogWaitTime = 0.03f;
			if (dialogIndex == 2) {
				dialogWaitTime = 0.325f;
			} else {
				if (frameCount % 5 == 0) Global.playSound("text");
			}
			frameCount++;

			if (dialogTime > dialogWaitTime) {
				dialogTime = 0;
				if (dialogLine1.Length < dialogLine1Content.Length) {
					dialogLine1 += dialogLine1Content[dialogIndex];
				} else if (dialogLine2.Length < dialogLine2Content.Length) {
					dialogLine2 += dialogLine2Content[dialogIndex - dialogLine1Content.Length];
				} else {
					state = 2;
					return;
				}
				dialogIndex++;
			}
		} else if (state == 2) {
			drLightAnim.frameSpeed = 0;
			drLightAnim.frameIndex = 0;
			subStateTime += Global.spf;
			if (subStateTime > 1.75f) {
				state = 3;
				dialogLine1 = "";
				dialogLine2 = "";
				dialogIndex = 0;
				subStateTime = 0;
			}
		} else if (state == 3) {
			drLightAnim.frameSpeed = 1;
			dialogTime += Global.spf;

			float dialogWaitTime = 0;
			if (dialogType == 0) {
				dialogWaitTime = 0;
				if (dialogIndex < 4) {
					dialogWaitTime = 0.03f;
					if (Global.frameCount % 5 == 0) Global.playSound("text");
				} else if (dialogIndex == 4) {
					dialogWaitTime = 0.4f;
				} else {
					if (Global.frameCount % 5 == 0) Global.playSound("text");
				}
			} else {
				dialogWaitTime = 0.03f;
				if (dialogIndex == dialogLine3Content.Length) {
					dialogWaitTime = 0.4f;
				} else {
					if (Global.frameCount % 5 == 0) Global.playSound("text");
				}
			}

			if (dialogTime > dialogWaitTime) {
				dialogTime = 0;
				if (dialogLine1.Length < dialogLine3Content.Length) {
					dialogLine1 += dialogLine3Content[dialogIndex];
				} else {
					state = 4;
					return;
				}
				dialogIndex++;
			}
		} else if (state == 4) {
			drLightAnim.frameSpeed = 0;
			drLightAnim.frameIndex = 0;
		}
		/*
		if (stateTime > 2) mmx.unpoShotCount = Math.Max(mmx.unpoShotCount, 1);
		if (stateTime > 3.75f) mmx.unpoShotCount = Math.Max(mmx.unpoShotCount, 2);
		if (stateTime > 5.75f) mmx.unpoShotCount = Math.Max(mmx.unpoShotCount, 3);
		*/
		if (stateTime > 7.75f) {
			//mmx.unpoShotCount = Math.Max(mmx.unpoShotCount, 4);
			character.changeState(new XRevive(), true);
		}
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		drLightAnim = new Anim(
			character.pos.addxy(30 * character.xDir, -15),
			"drlight", -character.xDir, player.getNextActorNetId(), false, sendRpc: true
		);
		drLightAnim.blink = true;
		int busterIndex = player.weapons.FindIndex(w => w is XBuster);
		if (busterIndex >= 0) {
			player.changeWeaponSlot(busterIndex);
		}
		mmx = character as RagingChargeX;
	}

	public override void onExit(CharState? newState) {
		base.onExit(newState);
		drLightAnim.destroySelf();
	}
}

public class XRevive : CharState {
	public float radius = 200;
	XReviveAnim reviveAnim = null!;
	RagingChargeX rcx = null!;

	public XRevive() : base("revive_shake") {
		invincible = true;
		immuneToWind = true;
		enterSound = "xRevive";
		useGravity = false;
	}

	public override void update() {
		base.update();
		if (!once && character.frameIndex >= 1 && sprite == "revive") {
			character.playSound("ching", sendRpc: true);
			player.health = 1;
			character.addHealth(player.maxHealth);
			once = true;
			var flash = new Anim(
				character.pos.addxy(0, -33), "up_flash",
				character.xDir, player.getNextActorNetId(), true, sendRpc: true
			);
			flash.grow = true;
		}
		if (sprite == "revive_shake" && character.loopCount > 6) {
			sprite = "revive_shake2";
			character.changeSpriteFromName(sprite, true);
			character.sprite.doesLoop = true;
		} else if (
			sprite == "revive_shake2" && character.loopCount > 6) {
			sprite = "revive";
			character.changeSpriteFromName(sprite, true);
		}
		if (once && character.isAnimOver()) {
			character.changeToIdleOrFall();
			return;
		}
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		character.sprite.doesLoop = true;
		reviveAnim = new XReviveAnim(character.getCenterPos(), player.getNextActorNetId(), sendRpc: true);
		rcx = character as RagingChargeX ?? throw new NullReferenceException();
	}

	public override void onExit(CharState? newState) {
		base.onExit(newState);
		character.useGravity = true;
		rcx.invulnTime = 2;
	}
}

public class XReviveAnim : Anim {
	public float startRadius = 150;

	public XReviveAnim(
		Point pos, ushort? netId = null, bool sendRpc = false, bool ownedByLocalPlayer = true
	) : base(
		pos, "empty", 1, netId, false, sendRpc, ownedByLocalPlayer
	) {
		ttl = 1f;
	}

	public override void update() {
		base.update();
	}

	public override void render(float x, float y) {
		base.render(x, y);
		DrawWrappers.DrawCircle(
			pos.x + x, pos.y + y, startRadius * (1 - (time / ttl.Value)),
			false, Color.White, 5, zIndex + 1, true, Color.White
		);
	}
}

public class RcxUpShot : RcxState {
	public bool shoot;
	public bool grounded;

	public RcxUpShot() : base("unpo_up_shot") {
		landSprite = "unpo_up_shot";
		airSprite = "unpo_up_air_shot";
		airMove = true;
		useDashJumpSpeed = false;
		attackCtrl = true;
	}

	public override void update() {
		base.update();

		// Ejecuta la lógica de disparo solo si el frame es suficiente.
		if (character.frameIndex >= 0 && !shoot) {
			// Marca que ya se disparó
			shoot = true;
			mmx.shootEx(-64);
		}

		// Si la animación terminó y han pasado al menos 0.3 segundos, cambia al estado idle o fall
		if (stateFrames >= Character.DefaultShootAnimTime) {
			character.changeToIdleOrFall();
		}
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		if (grounded) {
			sprite = landSprite;
			character.changeSpriteFromName(landSprite, true);
		} else  {
			sprite = airSprite;
			character.changeSpriteFromName(airSprite, true);
		}
	}
}


public class RcxDownShoot : RcxState {
	public bool shoot;
	public bool grounded;

	public RcxDownShoot() : base("unpo_down_shot") {
		landSprite = "unpo_down_shot";
		airMove = true;
		useDashJumpSpeed = false;
		attackCtrl = true;

	}

	public override void update() {
		base.update();

		// Ejecuta la lógica de disparo solo si el frame es suficiente.
		if (character.frameIndex >= 0 && !shoot) {
			// Marca que ya se disparó
			shoot = true;
			mmx.shootEx(64);
			character.vel.y = -300f;
		}

		// Si la animación terminó y han pasado al menos 0.3 segundos, cambia al estado idle o fall
		if (stateFrames >= Character.DefaultShootAnimTime) {
			character.changeToIdleOrFall();
		}
	}
}

public class KickChargeState : CharState {
	public float dashTime; //Tiempo del dash
	public float dustTime; //Generador de Polvo con tiempo

	public KickChargeState() : base("unpo_slide", "") {
		enterSound = "fsplasher";
	}

	public override void update() {
		base.update();
		character.move(new Point(character.xDir * 250, 0f));
		dashTime += Global.spf;
		if ((double)dashTime > 0.6) {
			character.changeState(new Idle());
			return;
		}
		if ((double)dustTime > 0.1) {
			dustTime = 0f;
			new Anim(character.pos.addxy(0f, -4f), "dust", character.xDir,
			base.player.getNextActorNetId(), destroyOnEnd: true, sendRpc: true);
		}
		if (player.input.isPressed(Control.Jump, player)) {
			character.changeToIdleOrFall();
			return;
		}
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		character.isDashing = true;
	}
	public override void onExit(CharState newState) {
		base.onExit(newState);
		character.useGravity = true;
	}
}

public class UnlimitedCrushState : CharState {
	public float GigaTime;
	public UnlimitedCrushState() : base("unpo_gigga") {
		enterSound = "gigaCrushLate";
		invincible = true;
	}

	public override void update() {
		base.update();
		if (base.player != null) {
			GigaTime += Global.spf;
			if ((double)GigaTime > 0.5) {
				character.changeState(new Idle());
			}
		}
	}
}
public class Chargedpunch : CharState {
	public float dashTime; //Tiempo del dash
	public float dustTime; //Generador de Polvo con tiempo

	public Chargedpunch() : base("unpo_punch2", "") {
		enterSound = "fsplasher";
	}

	public override void update() {
		base.update();
		character.move(new Point(character.xDir * 250, 0f));
		dashTime += Global.spf;
		if ((double)dashTime > 0.45) {
			character.changeState(new Idle());
			return;
		}
		if ((double)dustTime > 0.1) {
			dustTime = 0f;
			new Anim(character.pos.addxy(0f, -4f), "dust", character.xDir,
			base.player.getNextActorNetId(), destroyOnEnd: true, sendRpc: true);
		}
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		character.isDashing = true;
		character.useGravity = false;
	}
	public override void onExit(CharState newState) {
		base.onExit(newState);
		character.useGravity = true;
	}
}
