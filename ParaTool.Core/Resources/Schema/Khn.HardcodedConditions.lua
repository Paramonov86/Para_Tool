--These were assembled using information from https://gist.github.com/Norbyte/5628f9787b39741bfbe0918be4c823c2
--However, from my ventures into mapping condition functors to get rid of linter warnings in CommonConditions,
--Several methods ended up having additional overloads or alternative arguments that were not listed on that page.
--Therefore, it's important to keep watching base game examples for usages of these methods and keep up with the map

---@diagnostic disable: missing-return

---@overload fun(resourceType:string, cost:KhnFloat):Khn_ConditionResult
---@overload fun(resourceType:string, cost:KhnFloat, level:KhnInteger):Khn_ConditionResult
---@overload fun(resourceType:string, cost:KhnFloat, level:KhnInteger, isPercentage:boolean):Khn_ConditionResult
---@overload fun(resourceType:string, cost:KhnFloat, level:KhnInteger, isPercentage:boolean, ignoreResourceConsumeBoosts:boolean):Khn_ConditionResult
---@param resourceType string
---@param cost KhnFloat
---@param level KhnInteger
---@param isPercentage boolean
---@param ignoreResourceConsumeBoosts boolean
---@param target Khn_Entity
---@return Khn_ConditionResult
function HasActionResource(resourceType, cost, level, isPercentage, ignoreResourceConsumeBoosts, target) end

---@overload fun(proficiency:string)
---@param proficiency string
---@param target Khn_Entity
function HasProficiency(proficiency, target) end

---@overload fun(proficiency:string):Khn_ConditionResult
---@param proficiency string
---@param target Khn_Entity
---@return Khn_ConditionResult
function IsOfProficiencyGroup(proficiency, target) end

---@overload fun(ability:KhnAbility, skill:KhnSkill)
---@param ability KhnAbility
---@param skill KhnSkill
---@param target Khn_Entity
function HasProficiencyBonus(ability, skill, target) end

---@overload fun():Khn_ConditionResult
---@param target Khn_Entity
---@return Khn_ConditionResult
function HasShieldEquipped(target) end

---@overload fun(properties:KhnWeaponProperties):Khn_ConditionResult
---@param properties KhnWeaponProperties
---@param target Khn_Entity
---@return Khn_ConditionResult
function HasWeaponProperty(properties, target) end

---@overload fun(proficiency:string)
---@param proficiency string
---@param target Khn_Entity
function IsWeaponOfProficiencyGroup(proficiency, target) end

---@overload fun(weaponFlags:string)
---@overload fun(weaponFlags:string, offHand:boolean)
---@overload fun(weaponFlags:string, offHand:boolean, checkBothWeaponSets:boolean)
---@param weaponFlags string
---@param offHand boolean
---@param checkBothWeaponSets boolean
---@param target Khn_Entity
---@return Khn_ConditionResult
function WieldingWeapon(weaponFlags, offHand, checkBothWeaponSets, target) end

---@overload fun()
---@overload fun(target:Khn_Entity)
---@param target Khn_Entity
---@param source Khn_Entity
function CanHarm(target, source) end

---@overload fun()
---@param target Khn_Entity
function Locked(target) end

---@overload fun():Khn_ConditionResult --defaults to context.Target
---@param target Khn_Entity
---@return Khn_ConditionResult
function Combat(target) end

---@overload fun()
---@overload fun(target:Khn_Entity)
---@param target Khn_Entity
---@param source Khn_Entity
function FacingMe(target, source) end

---@overload fun()
---@param target Khn_Entity
function IsConcentrating(target) end

---@overload fun()
---@param target Khn_Entity
function IsMoving(target) end

---@overload fun()
---@param target Khn_Entity
function WearingArmor(target) end

---@overload fun(attribute:string)
---@param attribute string
---@param target Khn_Entity
---@return Khn_ConditionResult
function HasAttribute(attribute, target) end

---@overload fun()
---@param target Khn_Entity
function Grounded(target) end

---@param flag DamageFlags
function HasDamageEffectFlag(flag) end

---@overload fun(passiveName:string)
---@param passiveName string
---@param target Khn_Entity
---@return Khn_ConditionResult
function HasPassive(passiveName, target) end

---@param spellId string
function SpellId(spellId) end

---@param spellId string
function IsSpellChildOrVariantFromContext(spellId) end

---@param statusId string
function StatusId(statusId) end

---@param status string
---@param statusGroup string
function StatusHasStatusGroup(status, statusGroup) end

---@overload fun()
---@param target Khn_Entity
function IsDowned(target) end

---@param removeCause KhnStatusRemoveCause
function RemoveCause(removeCause) end

---@overload fun(instrumentType:KhnInstrumentType)
---@param instrumentType KhnInstrumentType
---@param target Khn_Entity
function HasInstrumentTypeEquipped(instrumentType, target) end

---@overload fun()
---@param target Khn_Entity
function IsProxy(target) end

---@overload fun()
---@overload fun(offHand:boolean)
---@param offHand boolean
---@param target Khn_Entity
function CanDisarmWieldingWeapon(offHand, target) end

---@overload fun()
---@overload fun(source:Khn_Entity)
---@param source Khn_Entity
---@param weapon Khn_Entity
function IsProficientWith(source, weapon) end

---@overload fun()
---@overload fun(source:Khn_Entity)
---@overload fun(source:Khn_Entity, checkRanged:boolean)
---@param source Khn_Entity
---@param checkRanged boolean
---@param checkOffHand boolean
function IsProficientWithEquippedWeapon(source, checkRanged, checkOffHand) end

---@overload fun()
---@param target Khn_Entity
function Dead(target) end

---@overload fun(flags:SpellFlags)
---@param flags SpellFlags
---@param source Khn_Entity
function HasSpellFlag(flags, source) end

---@overload fun(level:KhnInteger)
---@param level KhnInteger
---@param source Khn_Entity
function IsSpellLevel(level, source) end

---@overload fun():Khn_ConditionResult
---@param target Khn_Entity
---@return Khn_ConditionResult
function IsEquipped(target) end

---@overload fun()
---@param target Khn_Entity
function IsWeapon(target) end

---@overload fun()
---@param target Khn_Entity
function IsImprovisedWeapon(target) end

---@overload fun(slot:ItemSlot)
---@param target Khn_Entity
function EquipmentSlotIs(slot, target) end

---@overload fun(healingType:KhnHealingType)
---@param target Khn_Entity
function CanRegainHP(healingType, target) end

---@overload fun()
---@param target Khn_Entity
function HasVerbalComponentBlocked(target) end

---@overload fun()
---@param target Khn_Entity
function HasSpellCastBlocked(target) end

---@overload fun():Khn_ConditionResult
---@overload fun(source:Khn_Entity):Khn_ConditionResult
---@overload fun(source:Khn_Entity, target:Khn_Entity):Khn_ConditionResult
---@param source Khn_Entity
---@param target Khn_Entity
---@param respectLos boolean
---@return Khn_ConditionResult
function CanSee(source, target, respectLos) end

---@overload fun(passive:Khn_Entity, item:Khn_Entity)
---@param passive Khn_Entity
---@param item Khn_Entity
---@param source Khn_Entity
function IsPassiveSource(passive, item, source) end

---@overload fun(passive:Khn_Entity, item:Khn_Entity)
---@param passive Khn_Entity
---@param item Khn_Entity
---@param owner Khn_Entity
function IsPassiveOwner(passive, item, owner) end

---@overload fun(spell:string)
---@param spell string
---@param target Khn_Entity
function IsSpellAvailableFromClass(spell, target) end

---@overload fun()
---@param target Khn_Entity
function IsItemDisabled(target) end

---@overload fun()
---@param target Khn_Entity
function IsInActiveWeaponSet(target) end

---@param damageType KhnDamageType
function SpellDamageTypeIs(damageType) end

---@param category KhnSpellCategory
function SpellCategoryIs(category) end

---@param type SpellType
function SpellTypeIs(type) end

---@param school KhnSchool
function IsSpellOfSchool(school) end

---@overload fun(searchString:string)
---@param searchString string
---@param checkMetaConditions boolean
function HasStringInSpellRoll(searchString, checkMetaConditions) end

---@overload fun(searchString:string)
---@param searchString string
---@param checkMetaConditions boolean
function HasStringInFunctorConditions(searchString, checkMetaConditions) end

---@overload fun(searchString:string)
---@param searchString string
---@param checkMetaConditions boolean
function HasStringInSpellConditions(searchString, checkMetaConditions) end

function HasAoEConditions() end

function HasExtendableStatus() end

---@overload fun(useCostDesc:string)
---@overload fun(useCostDesc:string, applyModifications:boolean, source:Khn_Entity)
---@param useCostDesc string
---@param applyModifications boolean
---@param source Khn_Entity
function HasUseCosts(useCostDesc, applyModifications, source) end

---@overload fun(type:StatsFunctorType)
---@param type StatsFunctorType
---@param minDuration KhnFloat
function HasDuration(type, minDuration) end

---@param type StatsFunctorType
function HasFunctor(type) end

---@param inputString string
---@return Khn_ConditionResult
function PassiveHasStatus(inputString) return ConditionResult() end

---@param statusId string
---@return Khn_ConditionResult
function SpellHasStatus(statusId) return ConditionResult() end

function IsAnInstrumentRequired() end

---@overload fun()
---@param target Khn_Entity
function TurnBased(target) end

---@overload fun():Khn_ConditionResult
---@param target Khn_Entity
---@return Khn_ConditionResult
function Player(target) end

---@overload fun(tag:string):Khn_ConditionResult
---@param tag string
---@param target Khn_Entity
---@return Khn_ConditionResult
function Tagged(tag, target) return ConditionResult() end

---@overload fun(tagList:table)
---@param tagList table
---@param target Khn_Entity
function HasAnyTags(tagList, target) end

---@overload fun(tagList:table)
---@param tagList table
---@param target Khn_Entity
function HasNoTags(tagList, target) end

---@overload fun()
---@param target Khn_Entity
function Party(target) end

---@overload fun()
---@param target Khn_Entity
function Summon(target) end

---@overload fun()
---@overload fun(target:Khn_Entity)
---@param target Khn_Entity
---@param position Khn_Vector
function IsInSightPyramid(target, position) end

---@overload fun()
---@param target Khn_Entity
function FreshCorpse(target) end

---@overload fun(status:string)
---@overload fun(status:string, target:Khn_Entity)
---@param status string
---@param target Khn_Entity
---@param source Khn_Entity
function IsImmuneToStatus(status, target, source) end

---@overload fun()
---@param target Khn_Entity
function ActedThisRoundInCombat(target) end

---@overload fun()
---@param target Khn_Entity
function HadTurnInCombat(target) end

---@overload fun()
---@param target Khn_Entity
---@return Khn_ConditionResult
function IsSupply(target) end

---@overload fun()
---@param target Khn_Entity
function IsStoryItem(target) end

---@param modifierGuid string
---@param value any
function CheckRulesetModifier(modifierGuid, value) end

---@overload fun()
---@param target Khn_Entity
function HasAnySpells(target) end

---@overload fun()
---@overload fun(source:Khn_Entity)
---@overload fun(source:Khn_Entity, target:Khn_Entity)
---@param source Khn_Entity
---@param target Khn_Entity
---@param isMainHand boolean
function GetAttackAdvantage(source, target, isMainHand) end

---@overload fun():Khn_Entity
---@param target Khn_Entity
---@return Khn_Entity
function GetActiveArmor(target) end

---@param from Khn_Vector
---@param to Khn_Vector
function Distance(from, to) end

---@param from Khn_Vector
---@param target Khn_Entity
function DistanceToEntityHitBounds(from, target) end

---@overload fun()
---@param target Khn_Entity
function GetEquipmentSlot(target) end

---@overload fun(slot:string)
---@param slot string
---@param target Khn_Entity
---@return Khn_Entity
function GetItemInEquipmentSlot(slot, target) end

---@overload fun():Khn_Entity
---@param target Khn_Entity
---@return Khn_Entity
function GetAttackWeapon(target) end

---@overload fun():Khn_Entity
---@overload fun(source:Khn_Entity):Khn_Entity
---@param source Khn_Entity
---@param isMainHand boolean
---@return Khn_Entity
function GetActiveWeapon(source, isMainHand) end

---@overload fun(ability:KhnAbility)
---@param ability KhnAbility
---@param source Khn_Entity
function CalculateSpellDC(ability, source) end

---@overload fun()
---@param source Khn_Entity
function CalculateManeuverSaveDC(source) end

---@overload fun()
---@overload fun(source:Khn_Entity)
---@param source Khn_Entity
---@param topOwner boolean
function GetSummoner(source, topOwner) end

---@overload fun()
---@overload fun(source:Khn_Entity)
---@param source Khn_Entity
---@param topOwner boolean
function GetOwner(source, topOwner) end

---@overload fun()
---@overload fun(caster:Khn_Entity)
---@param caster Khn_Entity
---@param weapon Khn_Entity
function GetSpellTargetRadius(caster, weapon) end

---@overload fun()
---@param caster Khn_Entity
function GetSpellAreaRadius(caster) end

---@overload fun(statusId:string):number
---@param statusId string
---@param target Khn_Entity
---@return number
function GetStatusDuration(statusId, target) end

---@param diceAmount KhnInteger
---@param diceType KhnDiceType
---@param additionalValue KhnInteger
function Roll(diceAmount, diceType, additionalValue) end

--for chance calc: Attack(AttackType attackType, [bool advantage, bool disadvantage, Entity source, Vector3 sourcePosition, Entity target, Vector3 targetPosition]) end
--for condition checks: Attack(AttackType attackType, [bool advantage, bool disadvantage, Entity target, Vector3 targetPosition, Entity source, Vector3 sourcePosition]) end
---@overload fun(attackType:KhnAttackType)
---@overload fun(attackType:KhnAttackType, advantage:boolean)
---@overload fun(attackType:KhnAttackType, advantage:boolean, disadvantage:boolean)
---@overload fun(attackType:KhnAttackType, advantage:boolean, disadvantage:boolean, source:Khn_Entity)
---@overload fun(attackType:KhnAttackType, advantage:boolean, disadvantage:boolean, source:Khn_Entity, sourcePosition:Khn_Vector)
---@overload fun(attackType:KhnAttackType, advantage:boolean, disadvantage:boolean, source:Khn_Entity, sourcePosition:Khn_Vector, target:Khn_Entity)
---@param attackType KhnAttackType --Needs Mapping
---@param advantage boolean
---@param disadvantage boolean
---@param source Khn_Entity
---@param sourcePosition Khn_Vector
---@param target Khn_Entity
---@param targetPosition Khn_Vector
function Attack(attackType, advantage, disadvantage, source, sourcePosition, target, targetPosition) end

---@overload fun(ability:KhnAbility, dcOptions:RollOptions):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:RollOptions, advantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:RollOptions, advantage:boolean, disadvantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:KhnInteger):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:KhnInteger, advantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:KhnInteger, advantage:boolean, disadvantage:boolean, target:Khn_Entity):Khn_ConditionResult
---@param ability KhnAbility
---@param dcOptions RollOptions
---@param advantage boolean
---@param disadvantage boolean
---@param target Khn_Entity
---@return Khn_ConditionResult
function SavingThrow(ability, dcOptions, advantage, disadvantage, target) return ConditionResult() end

--Some things seem to be sending a ConditionResult instead of a boolean to advantage. Either this has more overloads or a ConditionResult can evaluate to its .Result natively TODO
---@overload fun(ability:KhnAbility, dcOptions:RollOptions):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:RollOptions, advantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:RollOptions, advantage:boolean, disadvantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:RollOptions, advantage:boolean, disadvantage:boolean, target:Khn_Entity):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:KhnInteger):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:KhnInteger, advantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:KhnInteger, advantage:boolean, disadvantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:KhnInteger, advantage:boolean, disadvantage:boolean, target:Khn_Entity):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dcOptions:KhnInteger, advantage:boolean, disadvantage:boolean, target:Khn_Entity, source:Khn_Entity):Khn_ConditionResult
---@param skill KhnSkill
---@param dcOptions RollOptions
---@param advantage boolean
---@param disadvantage boolean
---@param target Khn_Entity
---@param source Khn_Entity
---@return Khn_ConditionResult
function SkillCheck(skill, dcOptions, advantage, disadvantage, target, source) return ConditionResult() end

---@overload fun(ability:KhnSkill, dc:KhnInteger):Khn_ConditionResult
---@overload fun(ability:KhnSkill, dc:KhnInteger, advantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnSkill, dc:KhnInteger, advantage:boolean, disadvantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnSkill, dc:KhnInteger, advantage:boolean, disadvantage:boolean, additionalValue:KhnInteger):Khn_ConditionResult
---@overload fun(ability:KhnSkill, dc:KhnInteger, advantage:boolean, disadvantage:boolean, additionalValue:KhnInteger, target:Khn_Entity):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dc:KhnInteger):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dc:KhnInteger, advantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dc:KhnInteger, advantage:boolean, disadvantage:boolean):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dc:KhnInteger, advantage:boolean, disadvantage:boolean, additionalValue:KhnInteger):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dc:KhnInteger, advantage:boolean, disadvantage:boolean, additionalValue:KhnInteger, target:Khn_Entity):Khn_ConditionResult
---@overload fun(ability:KhnAbility, dc:KhnInteger, advantage:boolean, disadvantage:boolean, additionalValue:KhnInteger, target:Khn_Entity, source:Khn_Entity):Khn_ConditionResult
---@param skill KhnSkill
---@param dc KhnInteger
---@param advantage boolean
---@param disadvantage boolean
---@param additionalValue KhnInteger
---@param target Khn_Entity
---@param source Khn_Entity
---@return Khn_ConditionResult
function AbilityCheck(skill, dc, advantage, disadvantage, additionalValue, target, source) end

---@param ignoreFlags string
function CanShortRest(ignoreFlags) end

---@overload fun()
---@param target Khn_Entity
function GetTadpolePowersNumber(target) end

---@overload fun(power:string)
---@param power string
---@param target Khn_Entity
function HasTadpolePower(power, target) end

---@overload fun(permanentlyHostile:boolean)
---@param permanentlyHostile boolean
---@param target Khn_Entity
function IsInCombatWithHostilePartyMember(permanentlyHostile, target) end

---@overload fun()
---@param target Khn_Entity
function CrowdCharacter(target) end

--subsequent functions are possibly not fully mapped with overloads

---@param target Khn_Entity
---@param source Khn_Entity
function AreInSameCombat(target, source) end

---@param attribute any
---@param target Khn_Entity
function HasEquipmentWithAttribute(attribute, target) end

---@param target Khn_Entity
function IsCurrentTurnInCombat(target) end

function GetPreferredCastingAbility() end

---@param context Khn_Context
function HasContextFlag(context) end

---@overload fun()
---@overload fun(target:Khn_Entity)
---@param target Khn_Entity
---@param source Khn_Entity
function Ally(target, source) end

---@param target Khn_Entity
---@param source Khn_Entity
---@return Khn_ConditionResult
function Enemy(target, source) end

---@overload fun():Khn_ConditionResult
---@param target Khn_Entity
---@return Khn_ConditionResult
function Character(target) end

---@overload fun():Khn_ConditionResult
---@param target Khn_Entity
---@return Khn_ConditionResult
function Item(target) end

---@param target Khn_Entity
---@param source Khn_Entity
function SummonOwner(target, source) end

---@overload fun(gridStateStr:string, target:Khn_Entity):Khn_ConditionResult
---@param gridStateStr string
---@param target Khn_Entity
---@param checkProxies boolean --assumed
---@return Khn_ConditionResult
function InSurface(gridStateStr, target, checkProxies) end

---@overload fun(gridStateStr:string):Khn_ConditionResult
---@param gridStateStr string
---@param position Khn_Vector --assumed
---@param source Khn_Entity
---@return Khn_ConditionResult
function Surface(gridStateStr, position, source) end

---@param transformType any --needs mapping
---@param targetPosition Khn_Vector --assumed
---@param source Khn_Entity
function CanTransformSurface(transformType, targetPosition, source) end

---@overload fun(status:string):Khn_ConditionResult
---@param status string
---@param target Khn_Entity
---@param source Khn_Entity
---@param ignoreDeletingStatuses boolean --assumed
---@return Khn_ConditionResult
function HasStatus(status, target, source, ignoreDeletingStatuses) return ConditionResult() end

---@overload fun(hasAnyStatuses:table, hasAllStatuses:table, hasNoneStatuses:table):Khn_ConditionResult
---@overload fun(hasAnyStatuses:table, hasAllStatuses:table, hasNoneStatuses:table, target:Khn_Entity):Khn_ConditionResult
---@param hasAnyStatuses table
---@param hasAllStatuses table
---@param hasNoneStatuses table
---@param target Khn_Entity
---@param source Khn_Entity
---@param ignoreDeletingStatuses boolean
---@return Khn_ConditionResult
function HasAnyStatus(hasAnyStatuses, hasAllStatuses, hasNoneStatuses, target, source, ignoreDeletingStatuses) end

---@param target Khn_Entity
function CanPickup(target) end

---@param templateId any --needs mapping
---@param position Khn_Vector --assumed
---@param source Khn_Entity
function CanStand(templateId, position, source) end

---@param target Khn_Entity
function Immobilized(target) end

---@param race any --needs mapping
---@param getOriginalRace any --needs mapping
---@param target Khn_Entity
function Race(race, getOriginalRace, target) end

---@param target Khn_Entity
---@param checkClouds boolean --assumed
---@param checkGround boolean --assumed
function IsInSunlight(target, checkClouds, checkGround) end

---@param target Khn_Entity
---@param source Khn_Entity
---@param checkStack any --needs mapping
function CanMove(target, source, checkStack) end

---@param target Khn_Entity
function IsMovable(target) end

---@param target Khn_Entity
function IsUnimportant(target) end

---@param obscuredState any --needs mapping
---@param target Khn_Entity
function HasObscuredState(obscuredState, target) end

---@param obscuredState any --needs mapping
---@param position Khn_Vector
function HasObscuredStateAtPosition(obscuredState, position) end

---@param distance KhnFloat --assumed
---@param target Khn_Entity
---@param source Khn_Entity
function GetAlliesWithinRange(distance, target, source) end

---@param distance KhnFloat --assumed
---@param target Khn_Entity
---@param source Khn_Entity
function GetEnemiesWithinRange(distance, target, source) end

---@param target Khn_Entity
function GetItemsInInventory(target) end

function GetLiftingWeight(target, checkStacks) end
