﻿using System;

namespace MMXOnline;

public class RagingChargeX : Character {
	public int shotCount;
	public float punchCooldown;
	public float kickchargeCooldown;
	public float unlimitedcrushCooldown;
	public float saberCooldown;
	public float parryCooldown;
	public float maxParryCooldown = 60;
	public bool doSelfDamage;
	public float selfDamageCooldown;
	public float selfDamageMaxCooldown = 120;
	public Projectile? absorbedProj;
	public RagingChargeBuster ragingBuster;

	public RagingChargeX(Player player, float x, float y, int xDir,
		bool isVisible, ushort? netId, bool ownedByLocalPlayer,
		bool isWarpIn = true
	) : base(
		player, x, y, xDir, isVisible, netId, ownedByLocalPlayer, isWarpIn
	) {
		charId = CharIds.RagingChargeX;

		// Set self-damage to 4s by default.
		selfDamageCooldown = 60 * 4;

		// Add raging charge buster to the weapon. (So ammo bar is visible)
		ragingBuster = new RagingChargeBuster();
		weapons.Add(ragingBuster);
	}
	public override void preUpdate() {
		base.preUpdate();

		// Cooldowns.
		Helpers.decrementFrames(ref punchCooldown);
		Helpers.decrementFrames(ref saberCooldown);
		Helpers.decrementFrames(ref parryCooldown);
		Helpers.decrementFrames(ref unlimitedcrushCooldown);
		// For the shooting animation.
		if (shootAnimTime > 0) {
			shootAnimTime -= Global.speedMul;
			if (shootAnimTime <= 0) {
				shootAnimTime = 0;
				if (sprite.name == getSprite(charState.shootSpriteEx)) {
					changeSpriteFromName(charState.defaultSprite, false);
					if (charState is WallSlide) {
						frameIndex = sprite.totalFrameNum - 1;
					}
				}
			}
		}
	}

	public override void update() {
		base.update();

		// Charge.
		chargeLogic(null);
	}
	public override float getDashSpeed() {
		if (sprite.name == "mmx_unpo_grab_dash") {
        return base.getDashSpeed() * 1.15f;
    }
		return base.getDashSpeed();
	}

	public override void postUpdate() {
		base.postUpdate();

		if (!isDecayImmune() && invulnTime == 0) {
			if (selfDamageCooldown <= 0) {
				applyDamage(1, player, this, null, (int)ProjIds.SelfDmg);
				selfDamageCooldown = selfDamageMaxCooldown;
				playSound("healX3");
			} else {
				Helpers.decrementFrames(ref selfDamageCooldown);
			}
		}
	}

	public override bool attackCtrl() {
		// Parry con arma derecha
		if (player.input.isPressed(Control.WeaponRight, player) && parryCooldown == 0) {
			parryCooldown = 60;
			enterParry();
			return true;
		}
		// Grab.
		if (player.input.isHeld(Control.Special1, player) &&
			charState is Dash or AirDash && 
			sprite.name != getSprite("unpo_grab_dash")
		) {
			charState.isGrabbing = true;
			changeSpriteFromName("unpo_grab_dash", true);
			return true;
		}
		// Not-giga crush.
		if (player.input.isPressed(Control.WeaponLeft, player) && unlimitedcrushCooldown == 0) {
			unlimitedcrushCooldown = 150;
			changeState(new UnlimitedCrushState(), true);
			return true;
		}

		// Disparo regular
		if (player.input.isPressed(Control.Shoot, player) && punchCooldown <= 0) {
			if (ragingBuster.ammo >= ragingBuster.getAmmoUsage(0) && ragingBuster.shootCooldown <= 0) {
				ragingBuster.shootCooldown = 500;
				shoot();
				return true;
			}
		}
		if (player.input.isPressed(Control.Special1, player) && punchCooldown == 0) {
			punchCooldown = 20;
			changeState(new XUPPunchState(grounded), true);
			return true;
		}  if (chargeLevel == 0 && punchCooldown == 0 && player.input.isPressed(Control.Special1, player)) {
			punchCooldown = 20;
			changeState(new XUPPunchState(grounded), true);
			resetCharge();
			return true;
		}
		// Liberar el golpe cargado cuando el jugador suelta el botón
		if (!player.input.isHeld(Control.Special1, player)) {
			if (chargeLevel == 3) {
				 {
					changeState(new Chargedpunch(), true);
					resetCharge();
				}

			} else if (chargeLevel == 2 && saberCooldown == 0) {
				saberCooldown = 60;
				changeState(new X6SaberState(grounded), true);
				resetCharge();
				return true;
			} else if (chargeLevel == 1 && saberCooldown == 0) {
				saberCooldown = 60;
				changeState(new X6SaberState(grounded), true);
				resetCharge();
				return true;
			}
		}
		if (player.input.isHeld(Control.Down, player) && kickchargeCooldown == 0 && player.input.isPressed(Control.Dash, player)) {
			kickchargeCooldown = 0;
			changeState(new KickChargeState(), true);
			return true;
		}

		return base.attackCtrl();
	}


	// Shoot and set animation if posible.
	public void shoot() {
		if (player.input.isHeld(Control.Up, player)) {
			changeState(new RcxUpShot(), true);
			vel.y = 50f;
			return;
		}
		if (player.input.isHeld(Control.Down, player) && !grounded) {
			changeState(new RcxDownShoot(), true);
			vel.y = -400f;
			return;
		}
		string shootSprite = getSprite(charState.shootSpriteEx);
		if (!Global.sprites.ContainsKey(shootSprite)) {
			shootSprite = grounded ? getSprite("shoot") : getSprite("fall_shoot");
		}
		if (shootAnimTime == 0) {
			changeSprite(shootSprite, false);
		} else if (charState is Idle && !charState.inTransition()) {
			frameIndex = 0;
			frameTime = 0;
		}
		shootAnimTime = DefaultShootAnimTime;

		shootEx(0);
	}

	public void shootEx(int byteAngle) {
		ragingBuster.shoot(this, byteAngle);
		ragingBuster.addAmmo(-ragingBuster.getAmmoUsage(0), player);
		punchCooldown = 15;
	}

	public void enterParry() {
		if (absorbedProj != null) {
			changeState(new XUPParryProjState(absorbedProj, true, false), true);
			absorbedProj = null;
			return;
		}
		changeState(new XUPParryStartState(), true);
		return;
	}
	private int chargeLevel;
	public override void increaseCharge() {
		chargeTime += Global.speedMul;

		if (isCharging()) {
			if (chargeTime <= 45) {
				chargeLevel = 1; // Nivel de carga básico
			} else if (chargeTime >90  && chargeTime < 150) {
				chargeLevel = 2; // Nivel de carga medio
			} else if (chargeTime >= 150 && chargeTime < 210) {
				chargeLevel = 3; // Nivel de carga alto
			}
		}
	}
	public override int getDisplayChargeLevel() {
		return chargeLevel; // Devuelve el nivel de carga actual para sincronizar la visualización
	}

	public override void chargeGfx() {
		if (ownedByLocalPlayer) {
			chargeEffect.stop();
		}

		if (isCharging()) {
			chargeSound.play();

			if (!sprite.name.Contains("ra_hide")) {
				int level = getDisplayChargeLevel();
				var renderGfx = level switch {
					1 => RenderEffectType.ChargeBlue,
					2 => RenderEffectType.ChargeYellow,
					3 => RenderEffectType.ChargeOrange,
					_ => RenderEffectType.ChargeGreen
				};

				addRenderEffect(renderGfx, 2, 6);
			}
			chargeEffect.update(getDisplayChargeLevel(), chargeLevel);
		}
	}



	private void resetCharge() {
		chargeLevel = 0;
		chargeTime = 0;
	}
	public override bool chargeButtonHeld() {
		return player.input.isHeld(Control.Special1, player);
	}

	public override bool isNonDamageStatusImmune() {
		return true;
	}

	public bool isDecayImmune() {
		return charState is XUPGrabState
			or XUPParryMeleeState or XUPParryProjState
			or Hurt or GenericStun
			or VileMK2Grabbed
			or GenericGrabbedState;
	}

	public override int getHitboxMeleeId(Collider hitbox) {
		return (int)(sprite.name switch {
			"mmx_beam_saber2" or "mmx_beam_saber_air2" => MeleeIds.ZSaber,
			"mmx_unpo_grab_dash" => MeleeIds.DashGrab,
			"mmx_unpo_punch" or "mmx_unpo_air_punch" => MeleeIds.Punch,
			"mmx_unpo_slide" => MeleeIds.KickCharge,
			"mmx_unpo_gigga" => MeleeIds.UnlimitedCrush,
			"mmx_unpo_parry_start" => MeleeIds.ParryBlock,
			"mmx_unpo_punch2" => MeleeIds.Chargedpunch,
			_ => MeleeIds.None
		});
	}

	public override Projectile? getMeleeProjById(int id, Point projPos, bool addToLevel = true) {
		Projectile? proj = id switch {
			(int)MeleeIds.DashGrab => new GenericMeleeProj(
				RCXGrab.netWeapon, projPos, ProjIds.UPGrab, player,
				0, 0, 0, addToLevel: addToLevel
			),
			(int)MeleeIds.ParryBlock => new GenericMeleeProj(
				RCXParry.netWeapon, projPos, ProjIds.UPParryBlock, player,
				0, 0, 60, addToLevel: addToLevel
			),
			(int)MeleeIds.Punch => new GenericMeleeProj(
				RCXPunch.netWeapon, projPos, ProjIds.UPPunch, player,
				3, Global.halfFlinch, 20, addToLevel: addToLevel
			),
			(int)MeleeIds.KickCharge => new GenericMeleeProj(
				RCXKickCharge.netWeapon, projPos, ProjIds.KickCharge, player,
				3, Global.halfFlinch, 45, isDeflectShield: true, addToLevel: addToLevel
			),
			(int)MeleeIds.UnlimitedCrush => new GenericMeleeProj(
				UnlimitedCrush.netWeapon, projPos, ProjIds.UnlimitedCrush, player,
				1, Global.halfFlinch, 30, addToLevel: addToLevel
			),
			(int)MeleeIds.ZSaber => new GenericMeleeProj(
				ZXSaber.netWeapon, projPos, ProjIds.X6Saber, player,
				3, Global.halfFlinch, 30, addToLevel: addToLevel
			),
			(int)MeleeIds.Chargedpunch => new GenericMeleeProj(
	ZXSaber.netWeapon, projPos, ProjIds.Chargedpunch, player,
	3, Global.defFlinch, 20, addToLevel: addToLevel
),
			_ => null
		};
		return proj;
	}

	public override string getSprite(string spriteName) {
		return "mmx_" + spriteName;
	}
}

public enum MeleeIds {
	None = -1,
	DashGrab,
	ParryBlock,
	Punch,
	ZSaber,
	KickCharge,
	UnlimitedCrush,
	Chargedpunch,
}

