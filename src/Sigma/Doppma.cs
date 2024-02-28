using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MMXOnline;

public class Doppma : BaseSigma {
	public float sigma3FireballCooldown;

	public Doppma(
		Player player, float x, float y, int xDir,
		bool isVisible, ushort? netId,
		bool ownedByLocalPlayer, bool isWarpIn = true
	) : base(
		player, x, y, xDir, isVisible,
		netId, ownedByLocalPlayer, isWarpIn
	) {
		sigmaSaberMaxCooldown = 0.5f;
	}

	public override void update() {
		base.update();
		if (!ownedByLocalPlayer) {
			return;
		}
		player.sigmaFireWeapon.update();
		Helpers.decrementTime(ref sigma3FireballCooldown);
		Helpers.decrementTime(ref sigma3ShieldCooldown);

		if (!string.IsNullOrEmpty(charState?.shootSprite) &&
			sprite?.name?.EndsWith(charState.shootSprite) == true
		) {
			if (isAnimOver() && charState is not Sigma3Shoot) {
				changeSpriteFromName(charState.sprite, true);
			} else {
				var shootPOI = getFirstPOI();
				if (shootPOI != null && player.sigmaFireWeapon.shootTime == 0) {
					player.sigmaFireWeapon.shootTime = 0.15f;
					int upDownDir = MathF.Sign(player.input.getInputDir(player).y);
					float ang = getShootXDir() == 1 ? 0 : 180;
					if (charState.shootSprite.EndsWith("jump_shoot_downdiag")) {
						ang = getShootXDir() == 1 ? 45 : 135;
					}
					if (charState.shootSprite.EndsWith("jump_shoot_down")) {
						ang = 90;
					}
					if (ang != 0 && ang != 180) {
						upDownDir = 0;
					}
					playSound("sigma3shoot", sendRpc: true);
					new Sigma3FireProj(
						player.sigmaFireWeapon, shootPOI.Value,
						ang, upDownDir, player, player.getNextActorNetId(), sendRpc: true
					);
				}
			}
		}
	}

	public override Collider getBlockCollider() {
		Rect rect = Rect.createFromWH(0, 0, 23, 55);
		return new Collider(rect.getPoints(), false, this, false, false, HitboxFlag.Hurtbox, new Point(0, 0));
	}

	public override bool attackCtrl() {
		if (isAttacking() || isInvulnerableAttack()) {
			return false;
		}
		if (charState?.canAttack() != true || player.weapon is MaverickWeapon) {
			return false;
		}
		bool attackPressed = false;
		if (player.weapon is not AssassinBullet) {
			if (player.input.isPressed(Control.Shoot, player)) {
				attackPressed = true;
				framesSinceLastAttack = 0;
			} else {
				framesSinceLastAttack++;
			}
		}
		bool lenientAttackPressed = (attackPressed || framesSinceLastAttack < 5);

		// Shoot button attacks.
		if (lenientAttackPressed) {
			if (charState is LadderClimb) {
				if (player.input.isHeld(Control.Left, player)) {
					xDir = -1;
				} else if (player.input.isHeld(Control.Right, player)) {
					xDir = 1;
				}
			}

			if (!string.IsNullOrEmpty(charState.shootSprite) && player.sigmaFireWeapon.shootTime == 0
				&& !isSigmaShooting() && sigma3FireballCooldown == 0
			) {
				if (charState is Fall || charState is Jump || charState is WallKick) {
					changeState(new Sigma3Shoot(player.input.getInputDir(player)), true);
				} else if (charState is Idle || charState is Run || charState is Dash
					|| charState is SwordBlock
				) {
					changeState(new Sigma3Shoot(player.input.getInputDir(player)), true);
				}
				sigma3FireballCooldown = maxSigma3FireballCooldown;
				changeSpriteFromName(charState.shootSprite, true);
				return true;
			}
		}
		if (grounded && player.input.isPressed(Control.Special1, player) &&
			charState is not SigmaThrowShieldState && sigma3ShieldCooldown == 0
		) {
			sigma3ShieldCooldown = maxSigma3ShieldCooldown;
			changeState(new SigmaThrowShieldState(), true);
			return true;
		}
		return base.attackCtrl();
	}

	public override string getSprite(string spriteName) {
		return "sigma3_" + spriteName;
	}

	// This can run on both owners and non-owners. So data used must be in sync.
	public override Projectile getProjFromHitbox(Collider collider, Point centerPoint) {
		if (collider.name == "shield") {
			return new GenericMeleeProj(
				new Weapon(), centerPoint, ProjIds.Sigma3ShieldBlock, player,
				damage: 0, flinch: 0, hitCooldown: 1, isDeflectShield: true, isShield: true
			);
		}
		return base.getProjFromHitbox(collider, centerPoint);
	}
}