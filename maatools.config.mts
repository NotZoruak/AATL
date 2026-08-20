import type { FullConfig } from '@nekosu/maa-tools'

const config: FullConfig = {
    cwd: import.meta.dirname,
    maaVersion: 'latest',
    interfacePath: 'assets/interface.json',
    check: {
        override: {
            'duplicate-next': 'ignore',
        },
    },
}

export default config
