shader_type spatial;
// Shared look for palette-coloured voxels (terrain blocks and .vox models).
// Colour comes from vertex colours written as plain sRGB hex values; this shader converts
// them to linear, then gives every voxel a slightly different brightness so large flat
// areas read as many small cubes instead of one painted surface.

uniform float cell_size = 1.0;                 // size of one voxel in this mesh's units
uniform bool world_cells = false;              // true for terrain (pattern fixed in the world)
uniform float value_jitter : hint_range(0.0, 0.3) = 0.07;
uniform float roughness : hint_range(0.0, 1.0) = 0.95;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float emission_energy = 0.0;           // > 0 for glowing voxels

varying vec3 cell_pos;
varying vec3 cell_normal;

void vertex() {
	if (world_cells) {
		cell_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
		cell_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);
	} else {
		cell_pos = VERTEX;
		cell_normal = NORMAL;
	}
}

float hash13(vec3 p) {
	p = fract(p * 0.1031);
	p += dot(p, p.zyx + 31.32);
	return fract((p.x + p.y) * p.z);
}

void fragment() {
	vec3 srgb = COLOR.rgb;
	vec3 lin = mix(pow((srgb + 0.055) / 1.055, vec3(2.4)), srgb / 12.92, lessThan(srgb, vec3(0.04045)));
	// Step half a voxel into the surface so every face of one voxel gets the same value.
	vec3 cell = floor((cell_pos - cell_normal * cell_size * 0.5) / cell_size + 0.0005);
	float v = hash13(cell);
	lin *= 1.0 + (v - 0.5) * 2.0 * value_jitter;
	ALBEDO = lin;
	ROUGHNESS = roughness;
	METALLIC = metallic;
	EMISSION = lin * emission_energy;
}
