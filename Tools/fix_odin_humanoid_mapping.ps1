$Path = "C:\TeamProject\finalprojectServerSouls\Assets\05. Models\OdinPlayer\Model\Odin XVI(new).fbx.meta"
$Text = Get-Content -LiteralPath $Path -Raw

function New-HumanBoneBlock {
    param(
        [string] $BoneName,
        [string] $HumanName
    )

@"
    - boneName: $BoneName
      humanName: $HumanName
      limit:
        min: {x: 0, y: 0, z: 0}
        max: {x: 0, y: 0, z: 0}
        value: {x: 0, y: 0, z: 0}
        length: 0
        modified: 0
"@
}

$Mappings = @(
    @("pelvis", "Hips"),
    @("spine_01.x", "Spine"),
    @("spine_02.x", "Chest"),
    @("spine_03.x", "UpperChest"),
    @("neck.x", "Neck"),
    @("head.x", "Head"),

    @("c_shoulder.l", "LeftShoulder"),
    @("arm_twist.l", "LeftUpperArm"),
    @("forearm.l", "LeftLowerArm"),
    @("hand.l", "LeftHand"),

    @("c_shoulder.r", "RightShoulder"),
    @("arm_twist.r", "RightUpperArm"),
    @("forearm.r", "RightLowerArm"),
    @("hand.r", "RightHand"),

    @("c_thigh_b.l", "LeftUpperLeg"),
    @("c_leg_fk.l", "LeftLowerLeg"),
    @("foot.l", "LeftFoot"),
    @("toes_01.l", "LeftToes"),

    @("c_thigh_b.r", "RightUpperLeg"),
    @("c_leg_fk.r", "RightLowerLeg"),
    @("foot.r", "RightFoot"),
    @("toes_01.r", "RightToes")
)

$Human = ($Mappings | ForEach-Object { New-HumanBoneBlock -BoneName $_[0] -HumanName $_[1] }) -join "`n"

$Text = $Text -replace "autoGenerateAvatarMappingIfUnspecified: 1", "autoGenerateAvatarMappingIfUnspecified: 0"
$Text = $Text -replace "animationType: \d+", "animationType: 3"
$Text = $Text -replace "avatarSetup: \d+", "avatarSetup: 1"
$Text = [regex]::Replace(
    $Text,
    "(humanDescription:\r?\n    serializedVersion: 3\r?\n    human:)\r?\n(?:    - boneName:[\s\S]*?)(\r?\n    skeleton:)",
    "`$1`n$Human`$2"
)

Set-Content -LiteralPath $Path -Value $Text -Encoding UTF8
