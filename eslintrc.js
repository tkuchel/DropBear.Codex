module.exports = {
  root: true,
  env: {
    browser: true,
    es2021: true
  },
  extends: [
    'eslint:recommended'
  ],
  parserOptions: {
    ecmaVersion: 2021,
    sourceType: 'module'
  },
  rules: {
    'no-unused-vars': 'error',
    'no-undef': 'error',
    'no-console': ['warn', { allow: ['warn', 'error', 'debug'] }],
    'prefer-const': 'error',
    'no-var': 'error',
    'eqeqeq': ['error', 'always'],
    'no-multiple-empty-lines': ['error', { max: 1 }],
    'no-promise-executor-return': 'error',
    'require-await': 'error',
    'no-return-await': 'error',
    'no-async-promise-executor': 'error',
    'prefer-promise-reject-errors': 'error',
    'no-throw-literal': 'error',
    'no-floating-decimal': 'error',
    'no-use-before-define': ['error', { functions: false }]
  }
};