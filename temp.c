#include <mono/metadata/mono-config.h>
#include <mono/metadata/assembly.h>

#include <mono/jit/jit.h>

#define USE_DEFAULT_MONO_API_STRUCT
/* -*- C -*- */
/*
 * The structure below should contain pointers to all the Mono runtime functions used throughout ANY
 * of the generated C code. The reason for this is to avoid symbol load failures when the generated
 * bundle is used by a third party which embeds Mono and loads it dynamically (like
 * Xamarin.Android). Third parties should provide their own version of the structure - compatible
 * with this one. This is done this way so that we don't have to make the API here public in any way
 * or form and thus maintain freedom to break it as we see needed.
 *
 * Whenever ANY change to this structure is made, the `init_default_mono_api_struct` and
 * `validate_api_struct` functions found in `template_common.inc` MUST be updated.
 *
 * The `mkbundle_log_error` must be provided by the embedding third party in order to implement a
 * logging method specific to that third party (e.g. Xamarin.Android cannot use `fprintf` since it
 * won't show up in the logcat).
 */
typedef struct BundleMonoAPI
{
	void (*mono_register_bundled_assemblies) (const MonoBundledAssembly **assemblies);
	void (*mono_register_config_for_assembly) (const char* assembly_name, const char* config_xml);
	void (*mono_jit_set_aot_mode) (MonoAotMode mode);
	void (*mono_aot_register_module) (void** aot_info);
	void (*mono_config_parse_memory) (const char *buffer);
	void (*mono_register_machine_config) (const char *config_xml);
} BundleMonoAPI;

#ifdef USE_DEFAULT_MONO_API_STRUCT
#include <stdio.h>
#include <stdarg.h>

static void
mkbundle_log_error (const char *format, ...)
{
	va_list ap;

	va_start (ap, format);
	vfprintf (stderr, format, ap);
	va_end (ap);
}
#endif // USE_DEFAULT_MONO_API_STRUCT
#define USE_COMPRESSED_ASSEMBLY

typedef struct _compressed_data {
	MonoBundledAssembly assembly;
	int compressed_size;
} CompressedAssembly;

extern const unsigned char assembly_data_Creator_PostBuild_exe [];
static CompressedAssembly assembly_bundle_Creator_PostBuild_exe = {{"Creator_PostBuild.exe", assembly_data_Creator_PostBuild_exe, 29696}, 11486};

static const CompressedAssembly *compressed [] = {
	&assembly_bundle_Creator_PostBuild_exe,
	NULL
};

/* -*- C -*- */
#include <stdlib.h>

static BundleMonoAPI mono_api;

void initialize_mono_api (const BundleMonoAPI *info)
{
	if (info == NULL) {
		mkbundle_log_error ("mkbundle: missing Mono API info\n");
		exit (1);
	}

	mono_api.mono_register_bundled_assemblies = info->mono_register_bundled_assemblies;
	mono_api.mono_register_config_for_assembly = info->mono_register_config_for_assembly;
	mono_api.mono_jit_set_aot_mode = info->mono_jit_set_aot_mode;
	mono_api.mono_aot_register_module = info->mono_aot_register_module;
	mono_api.mono_config_parse_memory = info->mono_config_parse_memory;
	mono_api.mono_register_machine_config = info->mono_register_machine_config;
}

#ifdef USE_COMPRESSED_ASSEMBLY

static int
validate_api_pointer (const char *name, void *ptr)
{
	if (ptr != NULL)
		return 0;

	mkbundle_log_error ("mkbundle: Mono API pointer '%s' missing\n", name);
	return 1;
}

static void
validate_api_struct ()
{
	int missing = 0;

	missing += validate_api_pointer ("mono_register_bundled_assemblies", mono_api.mono_register_bundled_assemblies);
	missing += validate_api_pointer ("mono_register_config_for_assembly", mono_api.mono_register_config_for_assembly);
	missing += validate_api_pointer ("mono_jit_set_aot_mode", mono_api.mono_jit_set_aot_mode);
	missing += validate_api_pointer ("mono_aot_register_module", mono_api.mono_aot_register_module);
	missing += validate_api_pointer ("mono_config_parse_memory", mono_api.mono_config_parse_memory);
	missing += validate_api_pointer ("mono_register_machine_config", mono_api.mono_register_machine_config);

	if (missing <= 0)
		return;

	mkbundle_log_error ("mkbundle: bundle not initialized properly, %d Mono API pointers are missing\n", missing);
	exit (1);
}

static void
init_default_mono_api_struct ()
{
#ifdef USE_DEFAULT_MONO_API_STRUCT
	mono_api.mono_register_bundled_assemblies = mono_register_bundled_assemblies;
	mono_api.mono_register_config_for_assembly = mono_register_config_for_assembly;
	mono_api.mono_jit_set_aot_mode = mono_jit_set_aot_mode;
	mono_api.mono_aot_register_module = mono_aot_register_module;
	mono_api.mono_config_parse_memory = mono_config_parse_memory;
	mono_api.mono_register_machine_config = mono_register_machine_config;
#endif // USE_DEFAULT_MONO_API_STRUCT
}

#endif

#ifndef USE_COMPRESSED_ASSEMBLY

static void install_aot_modules (void) {

	mono_api.mono_jit_set_aot_mode (MONO_AOT_MODE_NORMAL);

}

#endif

static char *image_name = "Creator_PostBuild.exe";

static void install_dll_config_files (void) {

}

static const char *config_dir = NULL;
static MonoBundledAssembly **bundled;

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <zlib.h>

static int
my_inflate (const Byte *compr, uLong compr_len, Byte *uncompr, uLong uncompr_len)
{
	int err;
	z_stream stream;

	memset (&stream, 0, sizeof (z_stream));
	stream.next_in = (Byte *) compr;
	stream.avail_in = (uInt) compr_len;

	// http://www.zlib.net/manual.html
	err = inflateInit2 (&stream, 16+MAX_WBITS);
	if (err != Z_OK)
		return 1;

	for (;;) {
		stream.next_out = uncompr;
		stream.avail_out = (uInt) uncompr_len;
		err = inflate (&stream, Z_NO_FLUSH);
		if (err == Z_STREAM_END) break;
		if (err != Z_OK) {
			printf ("%d\n", err);
			return 2;
		}
	}

	err = inflateEnd (&stream);
	if (err != Z_OK)
		return 3;

	if (stream.total_out != uncompr_len)
		return 4;
	
	return 0;
}

void mono_mkbundle_init ()
{
	CompressedAssembly **ptr;
	MonoBundledAssembly **bundled_ptr;
	Bytef *buffer;
	int nbundles;

	init_default_mono_api_struct ();
	validate_api_struct ();
	install_dll_config_files ();

	ptr = (CompressedAssembly **) compressed;
	nbundles = 0;
	while (*ptr++ != NULL)
		nbundles++;

	bundled = (MonoBundledAssembly **) malloc (sizeof (MonoBundledAssembly *) * (nbundles + 1));
	if (bundled == NULL) {
		// May fail...
		mkbundle_log_error ("mkbundle: out of memory");
		exit (1);
	}

	bundled_ptr = bundled;
	ptr = (CompressedAssembly **) compressed;
	while (*ptr != NULL) {
		uLong real_size;
		uLongf zsize;
		int result;
		MonoBundledAssembly *current;

		real_size = (*ptr)->assembly.size;
		zsize = (*ptr)->compressed_size;
		buffer = (Bytef *) malloc (real_size);
		result = my_inflate ((*ptr)->assembly.data, zsize, buffer, real_size);
		if (result != 0) {
			fprintf (stderr, "mkbundle: Error %d decompressing data for %s\n", result, (*ptr)->assembly.name);
			exit (1);
		}
		(*ptr)->assembly.data = buffer;
		current = (MonoBundledAssembly *) malloc (sizeof (MonoBundledAssembly));
		memcpy (current, *ptr, sizeof (MonoBundledAssembly));
		current->name = (*ptr)->assembly.name;
		*bundled_ptr = current;
		bundled_ptr++;
		ptr++;
	}
	*bundled_ptr = NULL;
	mono_api.mono_register_bundled_assemblies((const MonoBundledAssembly **) bundled);
}
int mono_main (int argc, char* argv[]);

#include <stdlib.h>
#include <string.h>
#ifdef _WIN32
#include <windows.h>
#endif

static char **mono_options = NULL;

static int count_mono_options_args (void)
{
	const char *e = getenv ("MONO_BUNDLED_OPTIONS");
	const char *p, *q;
	int i, n;

	if (e == NULL)
		return 0;

	/* Don't bother with any quoting here. It is unlikely one would
	 * want to pass options containing spaces anyway.
	 */

	p = e;
	n = 1;
	while ((q = strchr (p, ' ')) != NULL) {
		n++;
		p = q + 1;
	}

	mono_options = malloc (sizeof (char *) * (n + 1));

	p = e;
	i = 0;
	while ((q = strchr (p, ' ')) != NULL) {
		mono_options[i] = malloc ((q - p) + 1);
		memcpy (mono_options[i], p, q - p);
		mono_options[i][q - p] = '\0';
		i++;
		p = q + 1;
	}
	mono_options[i++] = strdup (p);
	mono_options[i] = NULL;

	return n;
}


int main (int argc, char* argv[])
{
	char **newargs;
	int i, k = 0;

	newargs = (char **) malloc (sizeof (char *) * (argc + 2 + count_mono_options_args ()));

	newargs [k++] = argv [0];

	if (mono_options != NULL) {
		i = 0;
		while (mono_options[i] != NULL)
			newargs[k++] = mono_options[i++];
	}

	newargs [k++] = image_name;

	for (i = 1; i < argc; i++) {
		newargs [k++] = argv [i];
	}
	newargs [k] = NULL;
	
	if (config_dir != NULL && getenv ("MONO_CFG_DIR") == NULL)
		mono_set_dirs (getenv ("MONO_PATH"), config_dir);
	
	mono_mkbundle_init();

	return mono_main (k, newargs);
}
