# Third-party notices

This repository includes the third-party material below under the terms shown with it.

## Common passwords

`src/NCode.Identity.OpenId.Authentication/Passwords/common-passwords.txt`, the list of common passwords that the
password policy refuses, is derived from `Passwords/Common-Credentials/100k-most-used-passwords-NCSC.txt` of SecLists
(<https://github.com/danielmiessler/SecLists>) at commit `1a7bb9127eca9e6ff2fc0301c597fe6e16a0cb56`. It contains the
entries of at least eight characters, lower-cased and without duplicates, as `build/Update-CommonPasswords.ps1`
produces them.

The source is the UK National Cyber Security Centre's list of the 100,000 most common passwords in Have I Been Pwned's
Pwned Passwords (<https://haveibeenpwned.com/Passwords>), which is free and open source for everyone to use.

Contains public sector information licensed under the Open Government Licence v3.0.
See <https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/>.

SecLists is distributed under the following license:

```text
MIT License

Copyright (c) 2018 Daniel Miessler

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
